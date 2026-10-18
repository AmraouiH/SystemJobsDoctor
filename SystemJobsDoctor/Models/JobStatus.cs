using System.Collections.Generic;
using System.Drawing;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.UI;

namespace SystemJobsDoctor.Models
{
    /// <summary>
    /// statecode / statuscode values of the asyncoperation table.
    /// </summary>
    public static class JobStatus
    {
        // statecode
        public const int StateReady = 0;
        public const int StateSuspended = 1;
        public const int StateLocked = 2;
        public const int StateCompleted = 3;

        // statuscode
        public const int WaitingForResources = 0;
        public const int Waiting = 10;
        public const int InProgress = 20;
        public const int Pausing = 21;
        public const int Canceling = 22;
        public const int Succeeded = 30;
        public const int Failed = 31;
        public const int Canceled = 32;

        /// <summary>Every status, in the order used by the filters, tiles and charts.</summary>
        public static readonly int[] All = { WaitingForResources, Waiting, InProgress, Pausing, Canceling, Failed, Canceled, Succeeded };

        public static int StateOf(int status)
        {
            switch (status)
            {
                case WaitingForResources: return StateReady;
                case Waiting: return StateSuspended;
                case InProgress:
                case Pausing:
                case Canceling: return StateLocked;
                default: return StateCompleted;
            }
        }

        public static string Label(int status)
        {
            switch (status)
            {
                case WaitingForResources: return Strings.StatusWaitingForResources;
                case Waiting: return Strings.StatusSuspended;
                case InProgress: return Strings.StatusInProgress;
                case Pausing: return Strings.StatusPausing;
                case Canceling: return Strings.StatusCanceling;
                case Succeeded: return Strings.StatusSucceeded;
                case Failed: return Strings.StatusFailed;
                case Canceled: return Strings.StatusCanceled;
                default: return status.ToString();
            }
        }

        public static Color ColorOf(int status)
        {
            switch (status)
            {
                case WaitingForResources: return Theme.Waiting;
                case Waiting: return Theme.Suspended;
                case InProgress:
                case Pausing:
                case Canceling: return Theme.Running;
                case Succeeded: return Theme.Success;
                case Failed: return Theme.Danger;
                case Canceled: return Theme.Subtle;
                default: return Theme.Muted;
            }
        }

        /// <summary>A job that needs attention: failed, suspended or waiting with an error.</summary>
        public static bool IsProblem(JobRecord job)
        {
            return job.StatusCode == Failed
                || job.StateCode == StateSuspended
                || (job.StateCode == StateReady && job.HasError);
        }

        public static IEnumerable<int> Completed => new[] { Succeeded, Failed, Canceled };
    }
}
