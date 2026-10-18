using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    public class TraceLogEntry
    {
        public Guid Id { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string TypeName { get; set; }
        public string MessageName { get; set; }
        public string PrimaryEntity { get; set; }
        public string Mode { get; set; }
        public int? Depth { get; set; }
        public int? DurationMs { get; set; }
        public string MessageBlock { get; set; }
        public string ExceptionDetails { get; set; }
        public bool HasException => !string.IsNullOrWhiteSpace(ExceptionDetails);
    }

    /// <summary>
    /// Plug-in trace logs linked to a system job (same correlation id or request id).
    /// </summary>
    public class TraceLogService
    {
        private readonly IOrganizationService _service;

        public TraceLogService(IOrganizationService service)
        {
            _service = service;
        }

        public static bool CanSearch(JobRecord job) => job.CorrelationId.HasValue || job.RequestId.HasValue;

        public List<TraceLogEntry> GetTraces(JobRecord job)
        {
            var query = new QueryExpression("plugintracelog")
            {
                ColumnSet = new ColumnSet("createdon", "typename", "messagename", "primaryentity", "mode", "depth",
                    "performanceexecutionduration", "messageblock", "exceptiondetails"),
                TopCount = 200
            };
            query.AddOrder("createdon", OrderType.Ascending);

            var filter = new FilterExpression(LogicalOperator.Or);
            if (job.CorrelationId.HasValue) filter.AddCondition("correlationid", ConditionOperator.Equal, job.CorrelationId.Value);
            if (job.RequestId.HasValue) filter.AddCondition("requestid", ConditionOperator.Equal, job.RequestId.Value);
            query.Criteria.AddFilter(filter);

            return _service.RetrieveMultiple(query).Entities.Select(e => new TraceLogEntry
            {
                Id = e.Id,
                CreatedOn = e.Attributes.TryGetValue("createdon", out var created) && created is DateTime c ? c : (DateTime?)null,
                TypeName = FetchHelper.Get<string>(e, "typename"),
                MessageName = FetchHelper.Get<string>(e, "messagename"),
                PrimaryEntity = FetchHelper.Get<string>(e, "primaryentity"),
                Mode = e.FormattedValues.TryGetValue("mode", out var mode) ? mode : string.Empty,
                Depth = e.Attributes.TryGetValue("depth", out var depth) && depth is int d ? d : (int?)null,
                DurationMs = e.Attributes.TryGetValue("performanceexecutionduration", out var duration) && duration is int ms ? ms : (int?)null,
                MessageBlock = FetchHelper.Get<string>(e, "messageblock"),
                ExceptionDetails = FetchHelper.Get<string>(e, "exceptiondetails")
            }).ToList();
        }

        /// <summary>Is plug-in trace logging enabled (organization.plugintracelogsetting: 0 = Off, 1 = Exception, 2 = All)?</summary>
        public int? GetTraceSetting()
        {
            try
            {
                var query = new QueryExpression("organization") { ColumnSet = new ColumnSet("plugintracelogsetting"), TopCount = 1 };
                var organization = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
                return FetchHelper.GetOption(organization, "plugintracelogsetting");
            }
            catch
            {
                return null;
            }
        }
    }
}
