using System;
using System.Collections.Generic;
using System.Linq;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;
using SystemJobsDoctor.UI;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Computes the dashboard breakdowns, the error groups and the failing process ranking from the loaded jobs.
    /// Pure in-memory logic (no service call), so it can be re-run instantly after an action.
    /// </summary>
    public static class JobAnalyzer
    {
        public const int TopProcesses = 15;

        /// <summary>Beyond this span (days) the timeline groups jobs by week.</summary>
        public const int MaxDailyBuckets = 120;

        /// <summary>Signature and short error of every job that has an error.</summary>
        public static void ComputeSignatures(IEnumerable<JobRecord> jobs)
        {
            foreach (var job in jobs)
            {
                if (!job.HasError || job.StatusCode == JobStatus.Succeeded)
                {
                    job.Signature = null;
                    job.ShortError = null;
                    continue;
                }

                var source = !string.IsNullOrWhiteSpace(job.Message) ? job.Message : job.FriendlyMessage;
                var cause = ErrorNormalizer.ExtractCause(source);
                var signature = ErrorNormalizer.Normalize(cause);
                if (string.IsNullOrEmpty(signature))
                {
                    signature = job.ErrorCode.HasValue && job.ErrorCode != 0 ? Strings.ErrorWithCode(job.ErrorCodeHex) : Strings.NoErrorMessage;
                }

                job.ShortError = string.IsNullOrEmpty(cause) ? signature : cause;
                job.Signature = signature;
            }
        }

        public static void Analyze(AnalysisResult result)
        {
            var jobs = result.Jobs;
            ComputeSignatures(jobs);

            result.ByStatus = JobStatus.All
                .Select(status => new BreakdownItem
                {
                    Key = status.ToString(),
                    Label = JobStatus.Label(status),
                    Count = jobs.Count(j => j.StatusCode == status),
                    Color = JobStatus.ColorOf(status)
                })
                .Where(i => i.Count > 0)
                .ToList();

            result.ByType = jobs
                .GroupBy(j => j.OperationType)
                .Select(g => new BreakdownItem { Key = g.Key.ToString(), Label = g.First().OperationTypeLabel, Count = g.Count(), Color = Theme.Accent })
                .OrderByDescending(i => i.Count)
                .ToList();

            result.ByTable = jobs
                .GroupBy(j => j.RegardingTable ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BreakdownItem
                {
                    Key = g.Key,
                    Label = g.Key.Length == 0 ? Strings.NoTable : TableLabel(result, g.Key),
                    Detail = g.Key,
                    Count = g.Count(),
                    Color = Theme.TealDark
                })
                .OrderByDescending(i => i.Count)
                .ToList();

            result.Processes = jobs
                .GroupBy(j => j.ProcessKey)
                .Select(g =>
                {
                    var problems = g.Where(JobStatus.IsProblem).ToList();
                    var first = g.First();
                    return new ProcessStat
                    {
                        Key = g.Key,
                        Name = first.ProcessName,
                        Kind = first.ProcessKind,
                        ProcessId = first.ProcessId,
                        Total = g.Count(),
                        Failed = g.Count(j => j.StatusCode == JobStatus.Failed),
                        Suspended = g.Count(j => j.StateCode == JobStatus.StateSuspended),
                        DistinctErrors = problems.Where(j => j.Signature != null).Select(j => j.Signature).Distinct().Count(),
                        TopError = problems.Where(j => j.Signature != null)
                            .GroupBy(j => j.Signature).OrderByDescending(e => e.Count()).Select(e => e.Key).FirstOrDefault(),
                        LastFailure = problems.Max(j => j.CompletedOn ?? j.ModifiedOn ?? j.CreatedOn)
                    };
                })
                .OrderByDescending(p => p.Total)
                .ToList();

            result.ByProcess = result.Processes
                .Take(TopProcesses)
                .Select(p => new BreakdownItem
                {
                    Key = p.Key,
                    Label = p.Name,
                    Detail = Strings.ProcessKindLabel(p.Kind),
                    Count = p.Total,
                    Color = p.FailureRate >= 0.2 ? Theme.Danger : p.Problems > 0 ? Theme.Suspended : Theme.Accent
                })
                .ToList();

            result.ErrorGroups = BuildErrorGroups(jobs, result);
            result.Days = BuildTimeline(jobs);
        }

        public static List<ProcessStat> TopFailing(AnalysisResult result, int count)
        {
            return result.Processes
                .Where(p => p.Problems > 0)
                .OrderByDescending(p => p.Problems)
                .ThenByDescending(p => p.FailureRate)
                .Take(count)
                .ToList();
        }

        private static string TableLabel(AnalysisResult result, string logicalName)
        {
            return result.TableNames.TryGetValue(logicalName, out var display) && !string.IsNullOrEmpty(display) ? display : logicalName;
        }

        private static List<ErrorGroup> BuildErrorGroups(IEnumerable<JobRecord> jobs, AnalysisResult result)
        {
            return jobs
                .Where(j => j.Signature != null && JobStatus.IsProblem(j))
                .GroupBy(j => j.Signature)
                .Select(g =>
                {
                    var group = new ErrorGroup { Signature = g.Key };
                    group.Jobs.AddRange(g.OrderByDescending(j => j.CreatedOn));

                    var sample = group.Jobs.First();
                    group.SampleMessage = !string.IsNullOrWhiteSpace(sample.Message) ? sample.Message : sample.FriendlyMessage;
                    group.ErrorCodeHex = group.Jobs.Select(j => j.ErrorCodeHex).FirstOrDefault(c => c.Length > 0) ?? string.Empty;

                    var processes = g.GroupBy(j => j.ProcessKey).OrderByDescending(p => p.Count()).ToList();
                    group.ProcessCount = processes.Count;
                    group.TopProcess = processes.First().First().ProcessName;

                    var tables = g.Select(j => j.RegardingTable).Where(t => !string.IsNullOrEmpty(t))
                        .GroupBy(t => t, StringComparer.OrdinalIgnoreCase).OrderByDescending(t => t.Count()).Select(t => TableLabel(result, t.Key)).ToList();
                    group.TableCount = tables.Count;
                    group.Tables = string.Join(", ", tables.Take(3)) + (tables.Count > 3 ? $" (+{tables.Count - 3})" : string.Empty);

                    group.FirstSeen = g.Min(j => j.CreatedOn);
                    group.LastSeen = g.Max(j => j.CreatedOn);
                    group.FailedCount = g.Count(j => j.StatusCode == JobStatus.Failed);
                    group.SuspendedCount = g.Count(j => j.StateCode == JobStatus.StateSuspended);
                    return group;
                })
                .OrderByDescending(g => g.Count)
                .ToList();
        }

        private static List<DayBucket> BuildTimeline(List<JobRecord> jobs)
        {
            var dated = jobs.Where(j => j.CreatedOn.HasValue).ToList();
            if (dated.Count == 0) return new List<DayBucket>();

            var first = dated.Min(j => j.CreatedOn.Value).ToLocalTime().Date;
            var last = dated.Max(j => j.CreatedOn.Value).ToLocalTime().Date;
            var weekly = (last - first).TotalDays > MaxDailyBuckets;

            DateTime BucketOf(DateTime local)
            {
                var day = local.Date;
                if (!weekly) return day;
                var offset = ((int)day.DayOfWeek + 6) % 7; // Monday
                return day.AddDays(-offset);
            }

            var buckets = new SortedDictionary<DateTime, DayBucket>();
            for (var day = BucketOf(first); day <= last; day = day.AddDays(weekly ? 7 : 1))
            {
                buckets[day] = new DayBucket { Day = day };
            }

            foreach (var job in dated)
            {
                var key = BucketOf(job.CreatedOn.Value.ToLocalTime());
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new DayBucket { Day = key };
                    buckets[key] = bucket;
                }

                if (job.StatusCode == JobStatus.Succeeded) bucket.Succeeded++;
                else if (job.StatusCode == JobStatus.Failed) bucket.Failed++;
                else if (job.StateCode == JobStatus.StateSuspended) bucket.Suspended++;
                else if (job.StateCode == JobStatus.StateReady || job.StateCode == JobStatus.StateLocked) bucket.Waiting++;
                else bucket.Other++;
            }

            return buckets.Values.ToList();
        }
    }
}
