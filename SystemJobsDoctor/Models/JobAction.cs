using System;
using System.Collections.Generic;

namespace SystemJobsDoctor.Models
{
    public enum JobActionKind
    {
        Resume,
        Cancel,
        Postpone,
        Delete
    }

    /// <summary>
    /// A bulk action on a selection of jobs, split into eligible and skipped jobs.
    /// </summary>
    public class JobActionPlan
    {
        public JobActionKind Kind { get; set; }
        public List<JobRecord> Eligible { get; } = new List<JobRecord>();
        public List<JobRecord> Skipped { get; } = new List<JobRecord>();

        /// <summary>
        /// Resume: when the platform refuses to resume a failed workflow job, run the workflow again on its record
        /// (ExecuteWorkflowRequest, which creates a new system job).
        /// </summary>
        public bool RerunFailedWorkflows { get; set; } = true;

        /// <summary>New postponeuntil value (UTC) for <see cref="JobActionKind.Postpone"/>.</summary>
        public DateTime? PostponeUntilUtc { get; set; }

        public static bool IsEligible(JobActionKind kind, JobRecord job)
        {
            switch (kind)
            {
                case JobActionKind.Resume:
                    // Like the Resume button of Dynamics 365: suspended jobs, and failed jobs.
                    return job.StateCode == JobStatus.StateSuspended || job.StatusCode == JobStatus.Failed;
                case JobActionKind.Cancel:
                    return job.StateCode != JobStatus.StateCompleted && job.StatusCode != JobStatus.Canceling;
                case JobActionKind.Postpone:
                    return job.StateCode == JobStatus.StateReady || job.StateCode == JobStatus.StateSuspended;
                case JobActionKind.Delete:
                    return job.StateCode == JobStatus.StateCompleted;
                default:
                    return false;
            }
        }

        public static JobActionPlan Build(JobActionKind kind, IEnumerable<JobRecord> jobs)
        {
            var plan = new JobActionPlan { Kind = kind };
            foreach (var job in jobs)
            {
                (IsEligible(kind, job) ? plan.Eligible : plan.Skipped).Add(job);
            }
            return plan;
        }
    }

    /// <summary>
    /// Outcome of a bulk action.
    /// </summary>
    public class JobActionResult
    {
        public JobActionKind Kind { get; set; }
        public List<JobRecord> Succeeded { get; } = new List<JobRecord>();
        public List<KeyValuePair<JobRecord, string>> Failed { get; } = new List<KeyValuePair<JobRecord, string>>();

        /// <summary>Failed workflow jobs that could not be resumed and whose workflow was run again (new system job).</summary>
        public List<JobRecord> Rerun { get; } = new List<JobRecord>();
        public bool Canceled { get; set; }
    }

    /// <summary>
    /// Storage cleanup rule, turned into a bulk deletion job on asyncoperation.
    /// </summary>
    public class CleanupRule
    {
        public bool Succeeded { get; set; } = true;
        public bool Canceled { get; set; } = true;
        public bool Failed { get; set; }
        public int OlderThanDays { get; set; } = 30;
        public JobTypeFilter Type { get; set; }

        public List<int> Statuses
        {
            get
            {
                var statuses = new List<int>();
                if (Succeeded) statuses.Add(JobStatus.Succeeded);
                if (Canceled) statuses.Add(JobStatus.Canceled);
                if (Failed) statuses.Add(JobStatus.Failed);
                return statuses;
            }
        }
    }

    /// <summary>
    /// A bulk deletion job (bulkdeleteoperation) that targets system jobs.
    /// </summary>
    public class BulkDeleteJobInfo
    {
        public Guid Id { get; set; }
        public Guid? AsyncOperationId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? NextRun { get; set; }
        public bool IsRecurring { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}
