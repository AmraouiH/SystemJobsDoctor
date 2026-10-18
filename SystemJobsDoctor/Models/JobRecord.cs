using System;
using Microsoft.Xrm.Sdk;

namespace SystemJobsDoctor.Models
{
    public enum ProcessKind
    {
        Other,
        Workflow,
        PluginStep
    }

    /// <summary>
    /// One system job (asyncoperation row), flattened for the grids and the analysis.
    /// </summary>
    public class JobRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public int OperationType { get; set; }
        public string OperationTypeLabel { get; set; }

        public int StateCode { get; set; }
        public int StatusCode { get; set; }

        public string Message { get; set; }
        public string FriendlyMessage { get; set; }
        public int? ErrorCode { get; set; }

        public DateTime? CreatedOn { get; set; }
        public DateTime? StartedOn { get; set; }
        public DateTime? CompletedOn { get; set; }
        public DateTime? PostponeUntil { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public EntityReference Regarding { get; set; }
        public string RegardingTable => Regarding?.LogicalName;

        public string OwnerName { get; set; }
        public EntityReference Owner { get; set; }

        public Guid? ProcessId { get; set; }
        public string ProcessName { get; set; }
        public ProcessKind ProcessKind { get; set; }

        public string MessageName { get; set; }
        public int? RetryCount { get; set; }
        public int? Depth { get; set; }
        public Guid? CorrelationId { get; set; }
        public Guid? RequestId { get; set; }
        public double? ExecutionTimeSpan { get; set; }

        /// <summary>Normalized error signature (null when the job has no error).</summary>
        public string Signature { get; set; }

        /// <summary>First meaningful line of the error.</summary>
        public string ShortError { get; set; }

        public bool HasError => (ErrorCode.HasValue && ErrorCode.Value != 0) || !string.IsNullOrWhiteSpace(Message) || !string.IsNullOrWhiteSpace(FriendlyMessage);

        /// <summary>Process key used for grouping (id when known, otherwise the job name).</summary>
        public string ProcessKey => ProcessId.HasValue ? ProcessId.Value.ToString("N") : "name:" + (ProcessName ?? Name ?? string.Empty).ToLowerInvariant();

        public string ErrorCodeHex => ErrorCode.HasValue && ErrorCode.Value != 0 ? "0x" + ErrorCode.Value.ToString("X8") : string.Empty;

        public TimeSpan? Duration => StartedOn.HasValue && CompletedOn.HasValue ? CompletedOn - StartedOn : null;
    }
}
