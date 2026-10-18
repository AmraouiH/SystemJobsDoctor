using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Reads system jobs (asyncoperation) with FetchXML paging, plus the table metadata the analysis needs.
    /// </summary>
    public class JobQueryService
    {
        public const string EntityName = "asyncoperation";
        private const int PageSize = 5000;

        /// <summary>Columns wanted for the analysis; only those that exist in the organization are requested.</summary>
        private static readonly string[] WantedColumns =
        {
            "asyncoperationid", "name", "operationtype", "statecode", "statuscode", "message", "friendlymessage", "errorcode",
            "createdon", "startedon", "completedon", "postponeuntil", "modifiedon", "regardingobjectid", "ownerid",
            "workflowactivationid", "owningextensionid", "messagename", "retrycount", "depth", "correlationid", "requestid",
            "executiontimespan"
        };

        private readonly IOrganizationService _service;
        private HashSet<string> _availableColumns;
        private readonly Dictionary<string, int?> _objectTypeCodes = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public JobQueryService(IOrganizationService service)
        {
            _service = service;
        }

        #region Metadata

        private IEnumerable<string> Columns
        {
            get
            {
                if (_availableColumns == null)
                {
                    try
                    {
                        var response = (RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
                        {
                            LogicalName = EntityName,
                            EntityFilters = EntityFilters.Attributes
                        });
                        _availableColumns = new HashSet<string>(
                            response.EntityMetadata.Attributes
                                .Where(a => a.IsValidForRead != false && string.IsNullOrEmpty(a.AttributeOf))
                                .Select(a => a.LogicalName),
                            StringComparer.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        // Metadata not readable: fall back to the columns present in every version.
                        _availableColumns = new HashSet<string>(WantedColumns.Take(16), StringComparer.OrdinalIgnoreCase);
                    }
                }
                return WantedColumns.Where(c => _availableColumns.Contains(c));
            }
        }

        public bool HasColumn(string column)
        {
            return Columns.Contains(column, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Object type code of a table, used to filter regardingobjecttypecode.</summary>
        public int? GetObjectTypeCode(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName)) return null;
            if (_objectTypeCodes.TryGetValue(logicalName, out var cached)) return cached;

            int? code = null;
            try
            {
                var response = (RetrieveEntityResponse)_service.Execute(new RetrieveEntityRequest
                {
                    LogicalName = logicalName.Trim().ToLowerInvariant(),
                    EntityFilters = EntityFilters.Entity
                });
                code = response.EntityMetadata.ObjectTypeCode;
                _displayNames[logicalName] = response.EntityMetadata.DisplayName?.UserLocalizedLabel?.Label ?? logicalName;
            }
            catch
            {
                // Unknown table: the caller reports it.
            }
            _objectTypeCodes[logicalName] = code;
            return code;
        }

        /// <summary>
        /// Every table of the organization (logical name, display name, object type code), in one light metadata query.
        /// Also fills the caches used by the table filter and the display names.
        /// </summary>
        public List<TableInfo> LoadTables()
        {
            var query = new EntityQueryExpression
            {
                Properties = new MetadataPropertiesExpression("LogicalName", "DisplayName", "ObjectTypeCode"),
                Criteria = new MetadataFilterExpression(LogicalOperator.And)
            };
            query.Criteria.Conditions.Add(new MetadataConditionExpression("IsIntersect", MetadataConditionOperator.Equals, false));

            var response = (RetrieveMetadataChangesResponse)_service.Execute(new RetrieveMetadataChangesRequest { Query = query });
            var tables = response.EntityMetadata
                .Select(e => new TableInfo
                {
                    LogicalName = e.LogicalName,
                    DisplayName = e.DisplayName?.UserLocalizedLabel?.Label ?? e.DisplayName?.LocalizedLabels?.FirstOrDefault()?.Label ?? e.LogicalName,
                    ObjectTypeCode = e.ObjectTypeCode
                })
                .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            Prime(tables);
            return tables;
        }

        /// <summary>Reuses a table list loaded earlier (no metadata call).</summary>
        public void Prime(IEnumerable<TableInfo> tables)
        {
            if (tables == null) return;
            foreach (var table in tables)
            {
                _objectTypeCodes[table.LogicalName] = table.ObjectTypeCode;
                _displayNames[table.LogicalName] = table.DisplayName;
            }
        }

        /// <summary>Display names of the given tables (logical name when it cannot be read).</summary>
        public Dictionary<string, string> GetDisplayNames(IEnumerable<string> logicalNames, Action<string> progress)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in logicalNames.Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).Take(150))
            {
                if (!_displayNames.ContainsKey(name))
                {
                    progress?.Invoke(Strings.ReadingTableNames);
                    GetObjectTypeCode(name);
                }
                result[name] = _displayNames.TryGetValue(name, out var display) ? display : name;
            }
            return result;
        }

        #endregion

        #region Filters

        /// <summary>
        /// FetchXML conditions of a scope (the period applies to <see cref="JobFilter.DateAttribute"/>).
        /// </summary>
        public string BuildConditions(JobFilter filter, bool includeStatuses)
        {
            var builder = new StringBuilder();
            var dateAttribute = JobFilter.DateAttributes.Contains(filter.DateAttribute) ? filter.DateAttribute : "createdon";

            if (filter.FromUtc.HasValue) builder.Append(FetchHelper.Condition(dateAttribute, "ge", FetchHelper.Date(filter.FromUtc.Value)));
            if (filter.ToUtc.HasValue) builder.Append(FetchHelper.Condition(dateAttribute, "lt", FetchHelper.Date(filter.ToUtc.Value)));

            if (includeStatuses && filter.Statuses.Count > 0 && filter.Statuses.Count < JobStatus.All.Length)
            {
                builder.Append(FetchHelper.In("statuscode", filter.Statuses));
            }

            var types = JobFilter.OperationTypes(filter.Type);
            if (types.Length > 0)
            {
                builder.Append(FetchHelper.In("operationtype", types, filter.Type == JobTypeFilter.Other));
            }

            if (!string.IsNullOrWhiteSpace(filter.Table))
            {
                var code = GetObjectTypeCode(filter.Table);
                if (!code.HasValue) throw new InvalidOperationException(Strings.UnknownTable(filter.Table));
                builder.Append(FetchHelper.Condition("regardingobjecttypecode", "eq", code.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(filter.NameContains))
            {
                builder.Append(FetchHelper.Condition("name", "like", "%" + filter.NameContains.Trim() + "%"));
            }

            return builder.ToString();
        }

        #endregion

        #region Jobs

        /// <summary>
        /// Loads the most recent jobs of the scope (createdon descending), up to <see cref="JobFilter.MaxRecords"/>.
        /// </summary>
        public List<JobRecord> LoadJobs(JobFilter filter, Action<string> progress, out bool truncated, Func<bool> isCancelled = null)
        {
            var conditions = BuildConditions(filter, true);
            var orderAttribute = JobFilter.DateAttributes.Contains(filter.DateAttribute) ? filter.DateAttribute : "createdon";
            var attributes = string.Concat(Columns.Select(c => $"<attribute name=\"{c}\" />"));
            var jobs = new List<JobRecord>();
            var page = 1;
            string cookie = null;
            truncated = false;

            // Constant page size (changing it between pages is not reliable with a paging cookie): the surplus is trimmed.
            var count = Math.Max(1, Math.Min(PageSize, filter.MaxRecords));

            while (true)
            {
                var cookieAttribute = cookie == null ? string.Empty : $" paging-cookie=\"{FetchHelper.Escape(cookie)}\"";
                var fetch = $"<fetch count=\"{count}\" page=\"{page}\"{cookieAttribute}>" +
                            $"<entity name=\"{EntityName}\">{attributes}" +
                            $"<order attribute=\"{orderAttribute}\" descending=\"true\" />" +
                            $"<filter type=\"and\">{conditions}</filter>" +
                            "</entity></fetch>";

                var result = _service.RetrieveMultiple(new FetchExpression(fetch));
                jobs.AddRange(result.Entities.Select(ToJob));
                progress?.Invoke(Strings.LoadedJobs(jobs.Count));

                if (jobs.Count >= filter.MaxRecords)
                {
                    truncated = result.MoreRecords || jobs.Count > filter.MaxRecords;
                    if (jobs.Count > filter.MaxRecords) jobs.RemoveRange(filter.MaxRecords, jobs.Count - filter.MaxRecords);
                    break;
                }
                if (!result.MoreRecords) break;

                // Stopped by the user: keep the pages already loaded.
                if (isCancelled != null && isCancelled())
                {
                    truncated = true;
                    break;
                }

                cookie = result.PagingCookie;
                page++;
            }

            return jobs;
        }

        /// <summary>Re-reads one job (detail pane refresh after an action).</summary>
        public JobRecord Retrieve(Guid id)
        {
            var entity = _service.Retrieve(EntityName, id, new ColumnSet(Columns.ToArray()));
            return ToJob(entity);
        }

        /// <summary>Oldest createdon of the scope (lower bound of the counters' time slices).</summary>
        public DateTime? GetOldestCreatedOn(string conditions)
        {
            var fetch = "<fetch top=\"1\">" +
                        $"<entity name=\"{EntityName}\"><attribute name=\"createdon\" />" +
                        "<order attribute=\"createdon\" />" +
                        $"<filter type=\"and\">{conditions}</filter>" +
                        "</entity></fetch>";
            var entity = _service.RetrieveMultiple(new FetchExpression(fetch)).Entities.FirstOrDefault();
            return entity == null ? null : NullableDate(entity, "createdon");
        }

        public static JobRecord ToJob(Entity entity)
        {
            var workflow = FetchHelper.Get<EntityReference>(entity, "workflowactivationid");
            var step = FetchHelper.Get<EntityReference>(entity, "owningextensionid");
            var owner = FetchHelper.Get<EntityReference>(entity, "ownerid");
            var name = FetchHelper.Get<string>(entity, "name");

            var job = new JobRecord
            {
                Id = entity.Id,
                Name = name,
                OperationType = FetchHelper.GetOption(entity, "operationtype") ?? 0,
                StateCode = FetchHelper.GetOption(entity, "statecode") ?? 0,
                StatusCode = FetchHelper.GetOption(entity, "statuscode") ?? 0,
                Message = FetchHelper.Get<string>(entity, "message"),
                FriendlyMessage = FetchHelper.Get<string>(entity, "friendlymessage"),
                ErrorCode = NullableInt(entity, "errorcode"),
                CreatedOn = NullableDate(entity, "createdon"),
                StartedOn = NullableDate(entity, "startedon"),
                CompletedOn = NullableDate(entity, "completedon"),
                PostponeUntil = NullableDate(entity, "postponeuntil"),
                ModifiedOn = NullableDate(entity, "modifiedon"),
                Regarding = FetchHelper.Get<EntityReference>(entity, "regardingobjectid"),
                Owner = owner,
                OwnerName = owner?.Name,
                MessageName = FetchHelper.Get<string>(entity, "messagename"),
                RetryCount = NullableInt(entity, "retrycount"),
                Depth = NullableInt(entity, "depth"),
                CorrelationId = NullableGuid(entity, "correlationid"),
                RequestId = NullableGuid(entity, "requestid"),
                ExecutionTimeSpan = entity.Attributes.TryGetValue("executiontimespan", out var span) && span is double d ? d : (double?)null
            };

            job.OperationTypeLabel = entity.FormattedValues.TryGetValue("operationtype", out var typeLabel)
                ? typeLabel
                : Strings.OperationType(job.OperationType);

            if (workflow != null)
            {
                job.ProcessId = workflow.Id;
                job.ProcessName = string.IsNullOrEmpty(workflow.Name) ? name : workflow.Name;
                job.ProcessKind = ProcessKind.Workflow;
            }
            else if (step != null)
            {
                job.ProcessId = step.Id;
                job.ProcessName = string.IsNullOrEmpty(step.Name) ? name : step.Name;
                job.ProcessKind = ProcessKind.PluginStep;
            }
            else
            {
                job.ProcessName = string.IsNullOrEmpty(name) ? job.OperationTypeLabel : name;
                job.ProcessKind = ProcessKind.Other;
            }

            return job;
        }

        private static int? NullableInt(Entity entity, string attribute)
        {
            return entity.Attributes.TryGetValue(attribute, out var value) && value is int number ? number : (int?)null;
        }

        private static DateTime? NullableDate(Entity entity, string attribute)
        {
            return entity.Attributes.TryGetValue(attribute, out var value) && value is DateTime date ? date : (DateTime?)null;
        }

        private static Guid? NullableGuid(Entity entity, string attribute)
        {
            return entity.Attributes.TryGetValue(attribute, out var value) && value is Guid guid && guid != Guid.Empty ? guid : (Guid?)null;
        }

        #endregion

        /// <summary>Pretty-prints a FetchXML string (preview of the cleanup query).</summary>
        public static string FormatXml(string xml)
        {
            try
            {
                return XElement.Parse(xml).ToString();
            }
            catch
            {
                return xml;
            }
        }
    }
}
