using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Storage cleanup: table size, estimate of the jobs a rule would delete, and creation of the bulk deletion job.
    /// </summary>
    public class CleanupService
    {
        private readonly IOrganizationService _service;
        private readonly JobCountService _counter;

        public CleanupService(IOrganizationService service, JobCountService counter)
        {
            _service = service;
            _counter = counter;
        }

        /// <summary>
        /// Row count of asyncoperation from the platform snapshot (RetrieveTotalRecordCount, refreshed regularly by Dataverse).
        /// Null when the message is not available.
        /// </summary>
        public long? GetSnapshotRowCount()
        {
            try
            {
                var response = (RetrieveTotalRecordCountResponse)_service.Execute(new RetrieveTotalRecordCountRequest
                {
                    EntityNames = new[] { JobQueryService.EntityName }
                });
                return response.EntityRecordCountCollection.TryGetValue(JobQueryService.EntityName, out var count) ? count : (long?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Exact counts per status of the whole table (no date bound).</summary>
        /// <param name="expectedTotal">Row snapshot of the table: plans the time slices and gives the progress percentage.</param>
        public StatusCounts CountAll(Action<string, int> progress, long? expectedTotal)
        {
            return _counter.Count(string.Empty, null, DateTime.UtcNow.AddMinutes(5), progress, expectedTotal);
        }

        /// <summary>Counts per status of the jobs the rule would delete today.</summary>
        /// <param name="expectedTotal">Known number of jobs of the rule statuses (whole table count), or null.</param>
        public StatusCounts Estimate(CleanupRule rule, Action<string, int> progress, long? expectedTotal = null)
        {
            var cutoff = DateTime.UtcNow.Date.AddDays(-rule.OlderThanDays);
            var conditions = new StringBuilder()
                .Append(FetchHelper.Condition("statecode", "eq", JobStatus.StateCompleted.ToString()))
                .Append(FetchHelper.In("statuscode", rule.Statuses))
                .Append(FetchHelper.Condition("completedon", "lt", FetchHelper.Date(cutoff)));

            var types = JobFilter.OperationTypes(rule.Type);
            if (types.Length > 0) conditions.Append(FetchHelper.In("operationtype", types, rule.Type == JobTypeFilter.Other));

            // completedon < cutoff implies createdon < cutoff: the counter's time slices stop at the cutoff.
            return _counter.Count(conditions.ToString(), null, cutoff, progress, expectedTotal);
        }

        /// <summary>
        /// Query of the bulk deletion job. The age condition is relative (olderthan-x-days) so a recurring job
        /// keeps deleting the jobs that become old enough.
        /// </summary>
        public static QueryExpression BuildQuery(CleanupRule rule)
        {
            var query = new QueryExpression(JobQueryService.EntityName) { ColumnSet = new ColumnSet("asyncoperationid") };
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, JobStatus.StateCompleted);
            query.Criteria.AddCondition("statuscode", ConditionOperator.In, rule.Statuses.Cast<object>().ToArray());
            query.Criteria.AddCondition("completedon", ConditionOperator.OlderThanXDays, rule.OlderThanDays);

            var types = JobFilter.OperationTypes(rule.Type);
            if (types.Length > 0)
            {
                query.Criteria.AddCondition("operationtype", rule.Type == JobTypeFilter.Other ? ConditionOperator.NotIn : ConditionOperator.In,
                    types.Cast<object>().ToArray());
            }
            return query;
        }

        /// <summary>FetchXML equivalent of the bulk deletion query (preview shown to the user).</summary>
        public string PreviewFetchXml(CleanupRule rule)
        {
            try
            {
                var response = (QueryExpressionToFetchXmlResponse)_service.Execute(new QueryExpressionToFetchXmlRequest { Query = BuildQuery(rule) });
                return JobQueryService.FormatXml(response.FetchXml);
            }
            catch (Exception error)
            {
                return FetchHelper.ErrorMessage(error);
            }
        }

        /// <summary>Creates the bulk deletion job and returns the id of its system job.</summary>
        public Guid CreateBulkDelete(CleanupRule rule, string jobName, DateTime startUtc, string recurrencePattern)
        {
            var request = new BulkDeleteRequest
            {
                JobName = jobName,
                QuerySet = new[] { BuildQuery(rule) },
                StartDateTime = startUtc,
                RecurrencePattern = recurrencePattern ?? string.Empty,
                SendEmailNotification = false,
                ToRecipients = new Guid[0],
                CCRecipients = new Guid[0]
            };
            var response = (BulkDeleteResponse)_service.Execute(request);
            return response.JobId;
        }

        /// <summary>Most recent bulk deletion jobs whose query targets asyncoperation.</summary>
        public List<BulkDeleteJobInfo> GetSystemJobBulkDeletes()
        {
            EntityCollection rows;
            var withQuery = true;
            try
            {
                rows = Retrieve(true);
            }
            catch
            {
                withQuery = false;
                rows = Retrieve(false);
            }

            return rows.Entities
                .Where(e => !withQuery || (FetchHelper.Get<string>(e, "orderedquerysetxml") ?? string.Empty)
                    .IndexOf(JobQueryService.EntityName, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(e => new BulkDeleteJobInfo
                {
                    Id = e.Id,
                    AsyncOperationId = FetchHelper.Get<EntityReference>(e, "asyncoperationid")?.Id,
                    Name = FetchHelper.Get<string>(e, "name"),
                    Status = e.FormattedValues.TryGetValue("statuscode", out var status) ? status : string.Empty,
                    CreatedOn = e.Attributes.TryGetValue("createdon", out var created) && created is DateTime c ? c : (DateTime?)null,
                    NextRun = e.Attributes.TryGetValue("nextrun", out var next) && next is DateTime n ? n : (DateTime?)null,
                    IsRecurring = FetchHelper.Get<bool>(e, "isrecurring"),
                    SuccessCount = FetchHelper.Get<int>(e, "successcount"),
                    FailureCount = FetchHelper.Get<int>(e, "failurecount")
                })
                .ToList();
        }

        private EntityCollection Retrieve(bool withQuery)
        {
            var columns = new List<string> { "name", "statuscode", "createdon", "nextrun", "isrecurring", "successcount", "failurecount", "asyncoperationid" };
            if (withQuery) columns.Add("orderedquerysetxml");

            var query = new QueryExpression("bulkdeleteoperation") { ColumnSet = new ColumnSet(columns.ToArray()), TopCount = 100 };
            query.AddOrder("createdon", OrderType.Descending);
            return _service.RetrieveMultiple(query);
        }
    }
}
