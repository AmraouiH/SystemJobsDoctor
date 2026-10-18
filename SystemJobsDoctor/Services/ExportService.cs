using System;
using System.Collections.Generic;
using System.Linq;
using SystemJobsDoctor.Helpers;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Excel report of an analysis: summary, top 10 failing processes, error groups, every loaded job and the breakdowns.
    /// </summary>
    public static class ExportService
    {
        public static void Export(string path, AnalysisResult result, string organization)
        {
            var writer = new ExcelWriter();
            writer.AddSheet(Strings.SheetSummary, new[] { Strings.ColItem, Strings.ColValue }, Summary(result, organization), new double[] { 42, 60 });

            var top = JobAnalyzer.TopFailing(result, 10);
            writer.AddSheet(Strings.SheetTopFailing,
                new[] { "#", Strings.DetailProcess, Strings.DetailProcessKind, Strings.ColProblems, Strings.StatusFailed, Strings.StatusSuspended, Strings.ColTotal, Strings.ColFailureRate, Strings.ColDistinctErrors, Strings.ColTopError, Strings.ColLastFailure },
                top.Select((p, i) => new object[]
                {
                    i + 1, p.Name, Strings.ProcessKindLabel(p.Kind), p.Problems, p.Failed, p.Suspended, p.Total,
                    new ExcelWriter.Percentage(p.FailureRate), p.DistinctErrors, p.TopError, Local(p.LastFailure)
                }));

            writer.AddSheet(Strings.SheetErrorGroups,
                new[] { Strings.ColCount, Strings.StatusFailed, Strings.StatusSuspended, Strings.ColSignature, Strings.DetailErrorCode, Strings.ColTopProcess, Strings.ColProcesses, Strings.ColTables, Strings.ColFirstSeen, Strings.ColLastSeen, Strings.ColSampleMessage },
                result.ErrorGroups.Select(g => new object[]
                {
                    g.Count, g.FailedCount, g.SuspendedCount, g.Signature, g.ErrorCodeHex, g.TopProcess, g.ProcessCount, g.Tables,
                    Local(g.FirstSeen), Local(g.LastSeen), g.SampleMessage
                }),
                new double[] { 9, 9, 11, 70, 13, 36, 11, 30, 17, 17, 80 });

            writer.AddSheet(Strings.SheetJobs,
                new[]
                {
                    Strings.DetailJobId, Strings.ColName, Strings.DetailProcess, Strings.DetailProcessKind, Strings.ColType, Strings.ColStatus, Strings.ColTable,
                    Strings.ColRecordId, Strings.ColRecord, Strings.ColOwner, Strings.ColCreated, Strings.DetailStarted, Strings.ColCompleted, Strings.ColPostponeUntil,
                    Strings.ColRetries, Strings.DetailDepth, Strings.DetailErrorCode, Strings.ColSignature, Strings.ColError, Strings.DetailCorrelation
                },
                result.Jobs.Select(j => new object[]
                {
                    j.Id.ToString(), j.Name, j.ProcessName, Strings.ProcessKindLabel(j.ProcessKind), j.OperationTypeLabel, JobStatus.Label(j.StatusCode), j.RegardingTable,
                    j.Regarding?.Id.ToString(), j.Regarding?.Name, j.OwnerName, Local(j.CreatedOn), Local(j.StartedOn), Local(j.CompletedOn), Local(j.PostponeUntil),
                    j.RetryCount, j.Depth, j.ErrorCodeHex, j.Signature, j.ShortError, j.CorrelationId?.ToString()
                }),
                new double[] { 38, 40, 40, 14, 22, 20, 18, 38, 30, 22, 17, 17, 17, 17, 8, 8, 13, 60, 60, 38 });

            writer.AddSheet(Strings.SheetProcesses,
                new[] { Strings.DetailProcess, Strings.DetailProcessKind, Strings.ColTotal, Strings.StatusFailed, Strings.StatusSuspended, Strings.ColFailureRate, Strings.ColDistinctErrors, Strings.ColTopError },
                result.Processes.Select(p => new object[] { p.Name, Strings.ProcessKindLabel(p.Kind), p.Total, p.Failed, p.Suspended, new ExcelWriter.Percentage(p.FailureRate), p.DistinctErrors, p.TopError }));

            writer.AddSheet(Strings.SheetTables, new[] { Strings.ColTable, Strings.ColLogicalName, Strings.ColCount },
                result.ByTable.Select(t => new object[] { t.Label, t.Detail, t.Count }));

            writer.AddSheet(Strings.SheetTypes, new[] { Strings.ColType, Strings.ColCount },
                result.ByType.Select(t => new object[] { t.Label, t.Count }));

            writer.Save(path);
        }

        private static IEnumerable<object[]> Summary(AnalysisResult result, string organization)
        {
            var filter = result.Filter;
            yield return new object[] { Strings.AppTitle, Strings.AppSubtitle };
            yield return new object[] { Strings.SummaryOrganization, organization };
            yield return new object[] { Strings.SummaryAnalyzedOn, result.AnalyzedOn };
            yield return new object[] { Strings.SummaryPeriod + " — " + Strings.DateFieldName(filter.DateAttribute), $"{Local(filter.FromUtc)?.ToString("yyyy-MM-dd HH:mm") ?? "…"} → {Local(filter.ToUtc)?.ToString("yyyy-MM-dd HH:mm") ?? Strings.Now}" };
            yield return new object[] { Strings.StatusesLabel, string.Join(", ", filter.Statuses.Select(JobStatus.Label)) };
            yield return new object[] { Strings.TypeLabel, Strings.JobTypes[(int)filter.Type] };
            if (!string.IsNullOrEmpty(filter.Table)) yield return new object[] { Strings.TableLabel, filter.Table };
            if (!string.IsNullOrEmpty(filter.NameContains)) yield return new object[] { Strings.NameLabel, filter.NameContains };
            yield return new object[] { Strings.SummaryLoaded, result.Jobs.Count };
            if (result.Truncated) yield return new object[] { Strings.SummaryTruncated, Strings.Truncated(filter.MaxRecords) };
            yield return new object[] { Strings.SummaryProblems, result.Jobs.Count(JobStatus.IsProblem) };
            yield return new object[] { Strings.SummaryGroups, result.ErrorGroups.Count };

            if (result.ServerCounts != null)
            {
                yield return new object[] { string.Empty, string.Empty };
                yield return new object[] { Strings.SummaryServerCounts, result.ServerCounts.Approximate ? Strings.Approximate : Strings.Exact };
                foreach (var status in JobStatus.All)
                {
                    yield return new object[] { "  " + JobStatus.Label(status), result.ServerCounts[status] };
                }
                yield return new object[] { "  " + Strings.ColTotal, result.ServerCounts.Total };
            }
        }

        private static DateTime? Local(DateTime? utc) => utc?.ToLocalTime();
    }
}
