using System;
using System.Collections.Generic;

namespace SystemJobsDoctor.Models
{
    /// <summary>
    /// Job type filter of the scope bar.
    /// </summary>
    public enum JobTypeFilter
    {
        All,
        SystemEvent,
        Workflow,
        BulkDelete,
        Import,
        Rollup,
        Other
    }

    /// <summary>
    /// Scope of an analysis: period, statuses, type, table and process.
    /// </summary>
    public class JobFilter
    {
        public const int OperationSystemEvent = 1;
        public const int OperationImport = 5;
        public const int OperationWorkflow = 10;
        public const int OperationBulkDelete = 13;
        public const int OperationImportSubprocess = 17;
        public const int OperationBulkDeleteSubprocess = 23;
        public const int OperationCalculateRollup = 57;
        public const int OperationMassCalculateRollup = 58;

        /// <summary>Date column the period applies to: createdon (default), startedon or completedon.</summary>
        public string DateAttribute { get; set; } = "createdon";

        /// <summary>Lower bound on <see cref="DateAttribute"/> (UTC), null = no bound.</summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>Upper bound on <see cref="DateAttribute"/> (UTC), null = now.</summary>
        public DateTime? ToUtc { get; set; }

        public static readonly string[] DateAttributes = { "createdon", "startedon", "completedon" };

        public bool FiltersOnCreatedOn => string.Equals(DateAttribute, "createdon", StringComparison.OrdinalIgnoreCase);

        /// <summary>statuscode values; empty = every status.</summary>
        public List<int> Statuses { get; set; } = new List<int>();

        public JobTypeFilter Type { get; set; }

        /// <summary>Logical name of the regarding table (optional).</summary>
        public string Table { get; set; }

        /// <summary>Text contained in the job name (optional).</summary>
        public string NameContains { get; set; }

        public int MaxRecords { get; set; } = 20000;

        /// <summary>operationtype values of the type filter (empty = no filter); for <see cref="JobTypeFilter.Other"/> the values to exclude.</summary>
        public static int[] OperationTypes(JobTypeFilter type)
        {
            switch (type)
            {
                case JobTypeFilter.SystemEvent: return new[] { OperationSystemEvent };
                case JobTypeFilter.Workflow: return new[] { OperationWorkflow };
                case JobTypeFilter.BulkDelete: return new[] { OperationBulkDelete, OperationBulkDeleteSubprocess };
                case JobTypeFilter.Import: return new[] { OperationImport, OperationImportSubprocess };
                case JobTypeFilter.Rollup: return new[] { OperationCalculateRollup, OperationMassCalculateRollup };
                case JobTypeFilter.Other:
                    return new[]
                    {
                        OperationSystemEvent, OperationWorkflow, OperationBulkDelete, OperationBulkDeleteSubprocess,
                        OperationImport, OperationImportSubprocess, OperationCalculateRollup, OperationMassCalculateRollup
                    };
                default: return new int[0];
            }
        }

        public JobFilter Clone()
        {
            var clone = (JobFilter)MemberwiseClone();
            clone.Statuses = new List<int>(Statuses);
            return clone;
        }
    }
}

namespace SystemJobsDoctor.Models
{
    /// <summary>
    /// A table of the organization (table selectors).
    /// </summary>
    public class TableInfo
    {
        public string LogicalName { get; set; }
        public string DisplayName { get; set; }
        public int? ObjectTypeCode { get; set; }

        public override string ToString() => string.Equals(DisplayName, LogicalName, System.StringComparison.OrdinalIgnoreCase)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }
}
