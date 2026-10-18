using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Exact server-side counts of system jobs per statuscode.
    /// Aggregate FetchXML is limited to 50,000 rows. Large scopes are therefore counted by createdon time slices:
    /// <list type="bullet">
    /// <item>the slices are planned from the expected number of jobs (row snapshot), about 40,000 jobs each,
    /// instead of halving the range again and again (each refused aggregate costs as much as a successful one);</item>
    /// <item>a slice that is still too dense is cut in four;</item>
    /// <item>several slices are counted at the same time, each worker with its own connection.</item>
    /// </list>
    /// </summary>
    public class JobCountService
    {
        /// <summary>Planned number of jobs per slice (below the 50,000 limit to absorb an uneven distribution).</summary>
        public const int TargetSliceSize = 40000;

        /// <summary>A refused slice is cut in this many parts.</summary>
        private const int SplitFactor = 4;

        /// <summary>Below this slice width a limit error is no longer split (count marked as approximate).</summary>
        private static readonly TimeSpan MinimumSlice = TimeSpan.FromSeconds(30);

        private readonly IOrganizationService _service;
        private readonly JobQueryService _queries;

        /// <summary>Checked before each aggregate query: when it returns true, the count throws OperationCanceledException.</summary>
        public Func<bool> IsCancelled { get; set; }

        /// <summary>Aggregate queries sent at the same time (1 = sequential). Dataverse accepts 52 concurrent requests per user.</summary>
        public int Parallelism { get; set; } = 4;

        public JobCountService(IOrganizationService service, JobQueryService queries)
        {
            _service = service;
            _queries = queries;
        }

        /// <summary>Counts per status for a scope (the status filter of the scope is ignored).</summary>
        public StatusCounts CountByStatus(JobFilter filter, Action<string, int> progress)
        {
            var conditions = _queries.BuildConditions(filter, false);

            // The time slices are cut on createdon. When the period applies to another date (started on, completed on),
            // jobs created before the period can match: the slices then start at the oldest matching job.
            if (!filter.FiltersOnCreatedOn) return Count(conditions, null, DateTime.UtcNow.AddMinutes(5), progress);

            var to = filter.ToUtc ?? DateTime.UtcNow.AddMinutes(5);
            return Count(conditions, filter.FromUtc, to, progress);
        }

        /// <summary>
        /// Counts per status of the jobs matching <paramref name="conditions"/> created in [from, to[.
        /// When <paramref name="fromUtc"/> is null the oldest matching job gives the lower bound.
        /// <paramref name="expectedTotal"/> (row snapshot) plans the slices and gives the progress percentage.
        /// </summary>
        public StatusCounts Count(string conditions, DateTime? fromUtc, DateTime toUtc, Action<string, int> progress, long? expectedTotal = null)
        {
            var counts = new StatusCounts();

            // First try: a single aggregate over the whole scope (the usual case for small organizations).
            // Skipped when the snapshot already announces more than 50,000 jobs: it would be refused after reading 50,000 rows.
            var surelyTooMany = expectedTotal.HasValue && expectedTotal.Value > AggregateLimitRows * 1.2;
            if (!surelyTooMany && TryAggregate(_service, conditions, fromUtc, toUtc, counts))
            {
                return counts;
            }

            var from = fromUtc ?? _queries.GetOldestCreatedOn(conditions + FetchHelper.Condition("createdon", "lt", FetchHelper.Date(toUtc)));
            counts.QueryCount++;
            if (!from.HasValue) return counts;

            // More than 50,000 jobs: with the snapshot, enough slices of about 40,000 jobs; otherwise one per worker.
            var slices = expectedTotal.HasValue && expectedTotal.Value > AggregateLimitRows
                ? (int)Math.Min(4000, Math.Ceiling(expectedTotal.Value / (double)TargetSliceSize))
                : Math.Max(2, Parallelism); // unknown size: one slice per worker, refused slices are cut in four
            CountSlices(conditions, Split(from.Value.AddSeconds(-1), toUtc, Math.Max(2, slices)), counts, progress, expectedTotal);
            return counts;
        }

        private const int AggregateLimitRows = 50000;

        private static IEnumerable<Tuple<DateTime, DateTime>> Split(DateTime from, DateTime to, int parts)
        {
            var width = (to - from).Ticks / parts;
            for (var i = 0; i < parts; i++)
            {
                var start = from.AddTicks(width * i);
                var end = i == parts - 1 ? to : from.AddTicks(width * (i + 1));
                if (end > start) yield return Tuple.Create(start, end);
            }
        }

        /// <summary>
        /// Counts a queue of time slices with <see cref="Parallelism"/> workers. A refused slice goes back to the queue
        /// cut in four. The workers stop at the first error (re-thrown) or when the count is cancelled.
        /// </summary>
        private void CountSlices(string conditions, IEnumerable<Tuple<DateTime, DateTime>> initial, StatusCounts counts, Action<string, int> progress, long? expectedTotal)
        {
            var queue = new ConcurrentQueue<Tuple<DateTime, DateTime>>(initial);
            var pending = queue.Count;
            var sync = new object();
            Exception failure = null;
            var stop = false;

            void Work(IOrganizationService service)
            {
                while (!Volatile.Read(ref stop) && Volatile.Read(ref pending) > 0)
                {
                    if (!queue.TryDequeue(out var slice))
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    try
                    {
                        var local = new StatusCounts();
                        if (TryAggregate(service, conditions, slice.Item1, slice.Item2, local))
                        {
                            long total;
                            lock (sync)
                            {
                                counts.Merge(local);
                                total = counts.Total;
                            }
                            var percent = expectedTotal.HasValue && expectedTotal.Value > 0 ? (int)Math.Min(99, total * 100 / expectedTotal.Value) : 0;
                            progress?.Invoke(Strings.CountingJobs(total), percent);
                        }
                        else
                        {
                            lock (sync) counts.QueryCount += local.QueryCount;
                            if (slice.Item2 - slice.Item1 <= MinimumSlice)
                            {
                                // More than 50,000 jobs created in a few seconds: at least 50,000, status unknown (lower bound).
                                lock (sync)
                                {
                                    counts.Approximate = true;
                                    counts.Unattributed += AggregateLimitRows;
                                }
                            }
                            else
                            {
                                // Enqueued before this slice is marked as done, so the workers never see 0 pending too early.
                                var parts = Split(slice.Item1, slice.Item2, SplitFactor).ToList();
                                Interlocked.Add(ref pending, parts.Count);
                                foreach (var part in parts) queue.Enqueue(part);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        lock (sync)
                        {
                            if (failure == null) failure = error;
                        }
                        Volatile.Write(ref stop, true);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref pending);
                    }
                }
            }

            var workers = Math.Max(1, Math.Min(16, Parallelism));
            if (workers == 1)
            {
                Work(_service);
            }
            else
            {
                var services = Enumerable.Range(0, workers).Select(i => i == 0 ? _service : CloneService()).ToList();
                try
                {
                    var threads = services.Select(service => new Thread(() => Work(service)) { IsBackground = true }).ToList();
                    threads.ForEach(t => t.Start());
                    threads.ForEach(t => t.Join());
                }
                finally
                {
                    foreach (var service in services.Skip(1))
                    {
                        if (!ReferenceEquals(service, _service)) (service as IDisposable)?.Dispose();
                    }
                }
            }

            if (failure != null)
            {
                if (failure is OperationCanceledException) throw new OperationCanceledException();
                throw new InvalidOperationException(failure.Message, failure);
            }
        }

        /// <summary>
        /// A connection per worker (CrmServiceClient.Clone shares the authentication of the XrmToolBox connection).
        /// When the connection cannot be cloned, the workers share it (WCF channels accept concurrent calls).
        /// </summary>
        private IOrganizationService CloneService()
        {
            try
            {
                if (_service is CrmServiceClient client)
                {
                    var clone = client.Clone();
                    if (clone != null && clone.IsReady) return clone;
                    clone?.Dispose();
                }
            }
            catch
            {
                // Fall back to the shared connection.
            }
            return _service;
        }

        private bool TryAggregate(IOrganizationService service, string conditions, DateTime? from, DateTime? to, StatusCounts counts)
        {
            if (IsCancelled != null && IsCancelled()) throw new OperationCanceledException();

            var range = string.Empty;
            if (from.HasValue) range += FetchHelper.Condition("createdon", "ge", FetchHelper.Date(from.Value));
            if (to.HasValue) range += FetchHelper.Condition("createdon", "lt", FetchHelper.Date(to.Value));

            var fetch = "<fetch aggregate=\"true\">" +
                        $"<entity name=\"{JobQueryService.EntityName}\">" +
                        "<attribute name=\"statuscode\" alias=\"status\" groupby=\"true\" />" +
                        "<attribute name=\"asyncoperationid\" alias=\"total\" aggregate=\"count\" />" +
                        $"<filter type=\"and\">{conditions}{range}</filter>" +
                        "</entity></fetch>";

            counts.QueryCount++;
            EntityCollection result;
            try
            {
                result = service.RetrieveMultiple(new FetchExpression(fetch));
            }
            catch (Exception error) when (FetchHelper.IsAggregateLimit(error))
            {
                return false;
            }

            foreach (var row in result.Entities)
            {
                var status = FetchHelper.GetOption(row, "status");
                var total = FetchHelper.Get<object>(row, "total");
                if (!status.HasValue || total == null) continue;
                counts.Add(status.Value, Convert.ToInt64(total));
            }
            return true;
        }
    }
}
