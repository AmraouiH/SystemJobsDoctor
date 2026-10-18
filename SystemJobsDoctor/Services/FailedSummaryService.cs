using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    public class FailedSummaryOptions
    {
        public bool IncludeSuspended { get; set; }
        public string Organization { get; set; }

        /// <summary>Description of the jobs the summary is built from (all loaded jobs, or the filtered job list).</summary>
        public string ScopeLabel { get; set; }

        /// <summary>True when the jobs are every loaded job (the server count of failures is then meaningful).</summary>
        public bool IsWholeAnalysis { get; set; }
    }

    /// <summary>
    /// Plain-text summary of the failed (and optionally suspended) jobs: overview, top causes with a hint,
    /// failing processes, tables, and next steps. Ready to paste in an e-mail, a ticket or Teams.
    /// </summary>
    public static class FailedSummaryService
    {
        private const int TopCauses = 10;
        private const int TopProcesses = 10;

        public static bool IsIncluded(JobRecord job, bool includeSuspended)
        {
            return job.StatusCode == JobStatus.Failed || (includeSuspended && job.StateCode == JobStatus.StateSuspended);
        }

        public static string Build(IList<JobRecord> jobs, AnalysisResult result, FailedSummaryOptions options)
        {
            JobAnalyzer.ComputeSignatures(jobs);
            var failed = jobs.Where(j => IsIncluded(j, options.IncludeSuspended)).ToList();
            var text = new StringBuilder();
            var filter = result.Filter;

            text.AppendLine(Strings.SummaryTitle(options.IncludeSuspended, options.Organization));
            text.AppendLine(Strings.SummaryContext(result.AnalyzedOn, filter, options.ScopeLabel));
            text.AppendLine();

            if (failed.Count == 0)
            {
                text.AppendLine(Strings.SummaryNoFailure(options.IncludeSuspended));
                return text.ToString().TrimEnd();
            }

            // Overview
            var causes = failed.GroupBy(j => j.Signature ?? Strings.NoErrorMessage).OrderByDescending(g => g.Count()).ToList();
            var processes = jobs.GroupBy(j => j.ProcessKey)
                .Select(g => new { Jobs = g.ToList(), Failed = g.Count(j => IsIncluded(j, options.IncludeSuspended)) })
                .Where(p => p.Failed > 0)
                .OrderByDescending(p => p.Failed)
                .ToList();
            var tables = failed.GroupBy(j => j.RegardingTable ?? string.Empty, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ToList();
            var first = failed.Min(j => j.CreatedOn);
            var last = failed.Max(j => j.CreatedOn);
            var peak = failed.Where(j => j.CreatedOn.HasValue).GroupBy(j => j.CreatedOn.Value.ToLocalTime().Date).OrderByDescending(g => g.Count()).FirstOrDefault();

            text.AppendLine(Strings.SummaryOverviewHeader);
            text.AppendLine("• " + Strings.SummaryFailedCount(failed.Count, failed.Count(j => j.StatusCode == JobStatus.Failed), failed.Count(j => j.StateCode == JobStatus.StateSuspended),
                jobs.Count, options.IncludeSuspended));
            if (options.IsWholeAnalysis && result.ServerCounts != null)
            {
                var server = result.ServerCounts[JobStatus.Failed] + (options.IncludeSuspended ? result.ServerCounts[JobStatus.Waiting] : 0);
                text.AppendLine("• " + Strings.SummaryServerFailed(server, result.ServerCounts.Approximate));
            }
            text.AppendLine("• " + Strings.SummaryDistinct(causes.Count, processes.Count, tables.Count(t => t.Key.Length > 0)));
            text.AppendLine("• " + Strings.SummaryRange(first, last));
            if (peak != null) text.AppendLine("• " + Strings.SummaryPeak(peak.Key, peak.Count()));
            text.AppendLine();

            // Top causes
            text.AppendLine(Strings.SummaryCausesHeader(Math.Min(TopCauses, causes.Count), causes.Count));
            var rank = 0;
            foreach (var cause in causes.Take(TopCauses))
            {
                rank++;
                var items = cause.ToList();
                var mainProcesses = items.GroupBy(j => j.ProcessName ?? j.Name).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
                var causeTables = items.Select(j => j.RegardingTable).Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(t => TableLabel(result, t)).ToList();
                var code = items.Select(j => j.ErrorCodeHex).FirstOrDefault(c => c.Length > 0);

                text.AppendLine($"{rank}. {Strings.SummaryCauseLine(items.Count, (double)items.Count / failed.Count)} — {cause.Key}");
                var details = new List<string>
                {
                    Strings.SummaryProcess(mainProcesses.First(), mainProcesses.Count - 1)
                };
                if (causeTables.Count > 0) details.Add(Strings.SummaryTables(string.Join(", ", causeTables.Take(3)), causeTables.Count - 3));
                details.Add(Strings.SummaryLast(items.Max(j => j.CreatedOn)));
                if (!string.IsNullOrEmpty(code)) details.Add(code);
                text.AppendLine("   " + string.Join(" · ", details));

                var hint = ErrorHints.Detect(cause.Key);
                if (hint != HintKind.None) text.AppendLine("   → " + Strings.HintText(hint));
            }
            if (causes.Count > TopCauses)
            {
                text.AppendLine("   " + Strings.SummaryOtherCauses(causes.Count - TopCauses, causes.Skip(TopCauses).Sum(c => c.Count())));
            }
            text.AppendLine();

            // Processes
            text.AppendLine(Strings.SummaryProcessesHeader);
            rank = 0;
            foreach (var process in processes.Take(TopProcesses))
            {
                rank++;
                var sample = process.Jobs[0];
                var mainCause = process.Jobs.Where(j => IsIncluded(j, options.IncludeSuspended) && j.Signature != null)
                    .GroupBy(j => j.Signature).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                text.AppendLine($"{rank}. {sample.ProcessName} ({Strings.ProcessKindLabel(sample.ProcessKind)}) — " +
                                Strings.SummaryProcessLine(process.Failed, process.Jobs.Count, (double)process.Failed / process.Jobs.Count));
                if (mainCause != null) text.AppendLine("   " + Strings.SummaryMainCause(mainCause));
            }
            text.AppendLine();

            // Tables
            text.AppendLine(Strings.SummaryTablesHeader);
            text.AppendLine("• " + string.Join(" · ", tables.Take(12).Select(t => $"{(t.Key.Length == 0 ? Strings.NoTable : TableLabel(result, t.Key))}: {t.Count():N0}")));
            text.AppendLine();

            // Next steps
            text.AppendLine(Strings.SummaryNextHeader);
            var topShare = (double)causes[0].Count() / failed.Count;
            text.AppendLine("• " + Strings.SummaryNextTopCause(topShare, causes.Count));
            if (failed.Any(j => j.StateCode == JobStatus.StateSuspended)) text.AppendLine("• " + Strings.SummaryNextResume);
            if (failed.Any(j => j.StatusCode == JobStatus.Failed)) text.AppendLine("• " + Strings.SummaryNextCleanup);
            if (failed.Any(j => j.ProcessKind == ProcessKind.PluginStep)) text.AppendLine("• " + Strings.SummaryNextTraces);
            if (result.Truncated) text.AppendLine("• " + Strings.SummaryNextTruncated(filter.MaxRecords));

            return text.ToString().TrimEnd();
        }

        /// <summary>Maximum number of lines of the error message quoted in a job summary.</summary>
        private const int MaxMessageLines = 40;

        /// <summary>
        /// Summary of one job: what ran, on which record, why it failed (cause + hint), whether the same error hits other jobs,
        /// the full message, the links and the next steps.
        /// </summary>
        public static string BuildForJob(JobRecord job, AnalysisResult result, string organization, string organizationUrl)
        {
            JobAnalyzer.ComputeSignatures(new[] { job });
            var text = new StringBuilder();
            text.AppendLine(Strings.JobSummaryHeader(job.StatusCode, job.ProcessName ?? job.Name));
            if (!string.IsNullOrEmpty(organization)) text.AppendLine(organization);
            text.AppendLine();

            // What
            text.AppendLine(Strings.JobSummaryWhat);
            void Line(string label, string value)
            {
                if (!string.IsNullOrEmpty(value)) text.AppendLine($"• {label}: {value}");
            }
            Line(Strings.ColStatus, JobStatus.Label(job.StatusCode));
            Line(Strings.ColType, job.OperationTypeLabel);
            Line(Strings.DetailProcess, $"{job.ProcessName} ({Strings.ProcessKindLabel(job.ProcessKind)})");
            Line(Strings.DetailMessage, job.MessageName);
            if (job.Regarding != null)
            {
                Line(Strings.ColRecord, $"{TableLabel(result, job.Regarding.LogicalName)} « {job.Regarding.Name ?? job.Regarding.Id.ToString()} » ({job.Regarding.Id})");
            }
            Line(Strings.ColOwner, job.OwnerName);
            Line(Strings.ColCreated, SystemJobsDoctorControl.FormatDate(job.CreatedOn, true));
            Line(Strings.DetailStarted, SystemJobsDoctorControl.FormatDate(job.StartedOn, true));
            Line(Strings.ColCompleted, SystemJobsDoctorControl.FormatDate(job.CompletedOn, true));
            if (job.Duration.HasValue) Line(Strings.DetailDuration, $"{job.Duration.Value.TotalSeconds:N1} s");
            Line(Strings.ColPostponeUntil, SystemJobsDoctorControl.FormatDate(job.PostponeUntil, true));
            if (job.RetryCount > 0) Line(Strings.ColRetries, job.RetryCount.ToString());
            if (job.Depth > 1) Line(Strings.DetailDepth, job.Depth.ToString());
            Line(Strings.DetailErrorCode, job.ErrorCodeHex);
            Line(Strings.DetailCorrelation, job.CorrelationId?.ToString());
            Line(Strings.DetailJobId, job.Id.ToString());
            text.AppendLine();

            // Cause
            text.AppendLine(Strings.JobSummaryCause);
            var hint = HintKind.None;
            if (job.Signature == null)
            {
                text.AppendLine(Strings.JobSummaryNoError);
            }
            else
            {
                text.AppendLine(job.ShortError);
                if (job.Signature != job.ShortError) text.AppendLine(Strings.JobSummaryNormalized(job.Signature));
                hint = ErrorHints.Detect(job.Signature);
                if (hint != HintKind.None) text.AppendLine("→ " + Strings.HintText(hint));

                // Same error elsewhere
                text.AppendLine();
                text.AppendLine(Strings.JobSummarySameHeader);
                var same = result.Jobs.Where(j => j.Signature == job.Signature && JobStatus.IsProblem(j)).ToList();
                if (same.Count <= 1)
                {
                    text.AppendLine("• " + Strings.JobSummaryAlone);
                }
                else
                {
                    var records = same.Where(j => j.Regarding != null).Select(j => j.Regarding.Id).Distinct().Count();
                    var processes = same.Select(j => j.ProcessKey).Distinct().Count();
                    text.AppendLine("• " + Strings.JobSummarySame(same.Count, same.Count(j => j.StatusCode == JobStatus.Failed),
                        same.Count(j => j.StateCode == JobStatus.StateSuspended), records, processes));
                    text.AppendLine("• " + Strings.SummaryRange(same.Min(j => j.CreatedOn), same.Max(j => j.CreatedOn)));
                }
            }
            text.AppendLine();

            // Full message
            var message = string.Join(Environment.NewLine + Environment.NewLine,
                new[] { job.FriendlyMessage, job.Message }.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct());
            if (message.Length > 0)
            {
                text.AppendLine(Strings.JobSummaryMessageHeader);
                var lines = message.Replace("\r\n", "\n").Split('\n');
                foreach (var line in lines.Take(MaxMessageLines)) text.AppendLine(line.TrimEnd());
                if (lines.Length > MaxMessageLines) text.AppendLine(Strings.JobSummaryTruncated(lines.Length - MaxMessageLines));
                text.AppendLine();
            }

            // Links
            var links = new List<string>();
            var jobUrl = Helpers.UrlHelper.Record(organizationUrl, JobQueryService.EntityName, job.Id);
            if (jobUrl != null) links.Add($"• {Strings.JobSummaryLinkJob}: {jobUrl}");
            if (job.Regarding != null)
            {
                var recordUrl = Helpers.UrlHelper.Record(organizationUrl, job.Regarding.LogicalName, job.Regarding.Id);
                if (recordUrl != null) links.Add($"• {Strings.JobSummaryLinkRecord}: {recordUrl}");
            }
            if (job.ProcessId.HasValue && job.ProcessKind != ProcessKind.Other)
            {
                var processUrl = Helpers.UrlHelper.Record(organizationUrl, job.ProcessKind == ProcessKind.Workflow ? "workflow" : "sdkmessageprocessingstep", job.ProcessId.Value);
                if (processUrl != null) links.Add($"• {Strings.JobSummaryLinkProcess}: {processUrl}");
            }
            if (links.Count > 0)
            {
                text.AppendLine(Strings.JobSummaryLinks);
                foreach (var link in links) text.AppendLine(link);
                text.AppendLine();
            }

            // Next steps
            text.AppendLine(Strings.SummaryNextHeader);
            if (job.StateCode == JobStatus.StateSuspended) text.AppendLine("• " + Strings.JobSummaryNextResume);
            if (job.StatusCode == JobStatus.Failed) text.AppendLine("• " + Strings.JobSummaryNextRerun);
            if (job.RetryCount > 0) text.AppendLine("• " + Strings.JobSummaryRetries(job.RetryCount.Value));
            if (job.ProcessKind == ProcessKind.PluginStep && job.CorrelationId.HasValue) text.AppendLine("• " + Strings.JobSummaryNextTraces(job.CorrelationId.Value.ToString()));
            if (job.Signature != null && result.Jobs.Count(j => j.Signature == job.Signature) > 1) text.AppendLine("• " + Strings.JobSummaryNextSame);

            return text.ToString().TrimEnd();
        }

        private static string TableLabel(AnalysisResult result, string logicalName)
        {
            return result.TableNames.TryGetValue(logicalName, out var display) && !string.IsNullOrEmpty(display) && display != logicalName
                ? $"{display} ({logicalName})"
                : logicalName;
        }
    }
}
