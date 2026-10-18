using System;
using System.Collections.Generic;
using System.Drawing;

namespace SystemJobsDoctor.Models
{
    /// <summary>
    /// Jobs sharing the same normalized error: « 1 240 failures = 1 cause ».
    /// </summary>
    public class ErrorGroup
    {
        public string Signature { get; set; }
        public string SampleMessage { get; set; }
        public string ErrorCodeHex { get; set; }
        public List<JobRecord> Jobs { get; } = new List<JobRecord>();
        public int Count => Jobs.Count;
        public int ProcessCount { get; set; }
        public int TableCount { get; set; }
        public string TopProcess { get; set; }
        public string Tables { get; set; }
        public DateTime? FirstSeen { get; set; }
        public DateTime? LastSeen { get; set; }
        public int SuspendedCount { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>
    /// Failure statistics of one process (workflow, plugin step…).
    /// </summary>
    public class ProcessStat
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public ProcessKind Kind { get; set; }
        public Guid? ProcessId { get; set; }
        public int Total { get; set; }
        public int Failed { get; set; }
        public int Suspended { get; set; }
        public int Problems => Failed + Suspended;
        public double FailureRate => Total == 0 ? 0 : (double)Problems / Total;
        public int DistinctErrors { get; set; }
        public string TopError { get; set; }
        public DateTime? LastFailure { get; set; }
    }

    /// <summary>
    /// One bar of a breakdown chart.
    /// </summary>
    public class BreakdownItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Detail { get; set; }
        public int Count { get; set; }
        public Color Color { get; set; } = Color.Empty;
    }

    /// <summary>
    /// Jobs created on one day, split by status family (timeline chart).
    /// </summary>
    public class DayBucket
    {
        public DateTime Day { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Suspended { get; set; }
        public int Waiting { get; set; }
        public int Other { get; set; }
        public int Total => Succeeded + Failed + Suspended + Waiting + Other;
    }

    /// <summary>
    /// Result of an analysis: the loaded jobs and everything computed from them.
    /// </summary>
    public class AnalysisResult
    {
        public JobFilter Filter { get; set; }
        public List<JobRecord> Jobs { get; set; } = new List<JobRecord>();

        /// <summary>True when the user stopped the analysis: only the jobs loaded until then are analyzed.</summary>
        public bool Stopped { get; set; }

        /// <summary>True when more jobs match the filter than <see cref="JobFilter.MaxRecords"/>.</summary>
        public bool Truncated { get; set; }

        /// <summary>Exact server counts per statuscode for the scope (status filter ignored).</summary>
        public StatusCounts ServerCounts { get; set; }

        public List<ErrorGroup> ErrorGroups { get; set; } = new List<ErrorGroup>();
        public List<ProcessStat> Processes { get; set; } = new List<ProcessStat>();
        public List<BreakdownItem> ByStatus { get; set; } = new List<BreakdownItem>();
        public List<BreakdownItem> ByType { get; set; } = new List<BreakdownItem>();
        public List<BreakdownItem> ByTable { get; set; } = new List<BreakdownItem>();
        public List<BreakdownItem> ByProcess { get; set; } = new List<BreakdownItem>();
        public List<DayBucket> Days { get; set; } = new List<DayBucket>();

        /// <summary>Display names of the regarding tables (logical name → display name).</summary>
        public Dictionary<string, string> TableNames { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public DateTime AnalyzedOn { get; set; } = DateTime.Now;

        /// <summary>Tables of the organization (null when the metadata could not be read).</summary>
        public List<TableInfo> Tables { get; set; }
    }

    /// <summary>
    /// Server-side counts per statuscode. A count is a lower bound when <see cref="Approximate"/> is set
    /// (the aggregate limit could not be bypassed for a very small time slice).
    /// </summary>
    public class StatusCounts
    {
        public Dictionary<int, long> Counts { get; } = new Dictionary<int, long>();
        public bool Approximate { get; set; }
        public int QueryCount { get; set; }

        /// <summary>Jobs known to exist in slices that could not be counted per status (lower bound).</summary>
        public long Unattributed { get; set; }

        public long this[int status] => Counts.TryGetValue(status, out var value) ? value : 0;

        public long Total
        {
            get
            {
                var total = Unattributed;
                foreach (var value in Counts.Values) total += value;
                return total;
            }
        }

        public void Add(int status, long count)
        {
            Counts[status] = this[status] + count;
        }

        public void Merge(StatusCounts other)
        {
            foreach (var pair in other.Counts) Add(pair.Key, pair.Value);
            Approximate |= other.Approximate;
            Unattributed += other.Unattributed;
            QueryCount += other.QueryCount;
        }
    }
}
