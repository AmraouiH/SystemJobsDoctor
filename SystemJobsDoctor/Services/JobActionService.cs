using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.Services
{
    /// <summary>
    /// Bulk actions on system jobs (resume, cancel, postpone, delete), sent in ExecuteMultiple batches.
    /// asyncoperation does not support SetState: the state is changed with a plain Update of statecode / statuscode.
    /// </summary>
    public class JobActionService
    {
        private readonly IOrganizationService _service;
        private readonly int _batchSize;

        public JobActionService(IOrganizationService service, int batchSize)
        {
            _service = service;
            _batchSize = Math.Max(1, Math.Min(1000, batchSize));
        }

        public JobActionResult Execute(JobActionPlan plan, BackgroundWorker worker)
        {
            var result = new JobActionResult { Kind = plan.Kind };
            var jobs = plan.Eligible;
            var failedBefore = plan.Kind == JobActionKind.Resume
                ? new HashSet<Guid>(jobs.Where(j => j.StatusCode == JobStatus.Failed).Select(j => j.Id))
                : new HashSet<Guid>();

            for (var start = 0; start < jobs.Count; start += _batchSize)
            {
                if (worker != null && worker.CancellationPending)
                {
                    result.Canceled = true;
                    break;
                }

                var batch = jobs.Skip(start).Take(_batchSize).ToList();
                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = false },
                    Requests = new OrganizationRequestCollection()
                };
                request.Requests.AddRange(batch.Select(job => BuildRequest(plan, job)));

                try
                {
                    var response = (ExecuteMultipleResponse)_service.Execute(request);
                    var faults = response.Responses
                        .Where(r => r.Fault != null)
                        .ToDictionary(r => r.RequestIndex, r => r.Fault.Message);

                    for (var i = 0; i < batch.Count; i++)
                    {
                        if (faults.TryGetValue(i, out var message)) result.Failed.Add(new KeyValuePair<JobRecord, string>(batch[i], message));
                        else
                        {
                            Apply(plan, batch[i]);
                            result.Succeeded.Add(batch[i]);
                        }
                    }
                }
                catch (Exception error)
                {
                    var message = FetchHelper.ErrorMessage(error);
                    result.Failed.AddRange(batch.Select(job => new KeyValuePair<JobRecord, string>(job, message)));
                }

                var done = Math.Min(jobs.Count, start + batch.Count);
                worker?.ReportProgress(jobs.Count == 0 ? 100 : done * 100 / jobs.Count, Strings.ActionProgress(plan.Kind, done, jobs.Count));
            }

            if (failedBefore.Count > 0 && !result.Canceled)
            {
                FollowUpFailedResumes(plan, result, failedBefore, worker);
            }

            return result;
        }

        /// <summary>
        /// A failed job is completed: the platform may refuse the resume, or accept the update without restarting the job.
        /// The state of the resumed failed jobs is read again; those still completed are run again when they are workflows
        /// (and the option is on), otherwise reported as failures.
        /// </summary>
        private void FollowUpFailedResumes(JobActionPlan plan, JobActionResult result, HashSet<Guid> failedBefore, BackgroundWorker worker)
        {
            worker?.ReportProgress(0, Strings.CheckingResumes);

            // Update accepted: check the job really left the Completed state.
            var accepted = result.Succeeded.Where(j => failedBefore.Contains(j.Id)).ToList();
            var stillCompleted = new HashSet<Guid>();
            foreach (var chunk in Chunks(accepted.Select(j => j.Id).ToList(), 200))
            {
                var query = new QueryExpression(JobQueryService.EntityName) { ColumnSet = new ColumnSet("statecode") };
                query.Criteria.AddCondition("asyncoperationid", ConditionOperator.In, chunk.Cast<object>().ToArray());
                foreach (var entity in _service.RetrieveMultiple(query).Entities)
                {
                    if (FetchHelper.GetOption(entity, "statecode") == JobStatus.StateCompleted) stillCompleted.Add(entity.Id);
                }
            }

            var notResumed = new List<KeyValuePair<JobRecord, string>>();
            foreach (var job in accepted.Where(j => stillCompleted.Contains(j.Id)))
            {
                result.Succeeded.Remove(job);
                job.StateCode = JobStatus.StateCompleted;
                job.StatusCode = JobStatus.Failed;
                notResumed.Add(new KeyValuePair<JobRecord, string>(job, Strings.ResumeRefused));
            }

            // Update refused by the platform.
            foreach (var failure in result.Failed.Where(f => failedBefore.Contains(f.Key.Id)).ToList())
            {
                result.Failed.Remove(failure);
                notResumed.Add(failure);
            }

            var rerunnable = plan.RerunFailedWorkflows
                ? notResumed.Where(f => f.Key.ProcessKind == ProcessKind.Workflow && f.Key.ProcessId.HasValue && f.Key.Regarding != null).ToList()
                : new List<KeyValuePair<JobRecord, string>>();
            foreach (var failure in notResumed.Except(rerunnable))
            {
                result.Failed.Add(new KeyValuePair<JobRecord, string>(failure.Key,
                    failure.Key.ProcessKind == ProcessKind.Workflow ? failure.Value : Strings.ResumeRefusedNotWorkflow(failure.Value)));
            }
            if (rerunnable.Count == 0) return;

            // Run the workflow again on the record (the definition of the activation that ran the job).
            var definitions = GetWorkflowDefinitions(rerunnable.Select(f => f.Key.ProcessId.Value).Distinct().ToList());
            var jobs = rerunnable.Select(f => f.Key).ToList();
            for (var start = 0; start < jobs.Count; start += _batchSize)
            {
                if (worker != null && worker.CancellationPending)
                {
                    result.Canceled = true;
                    result.Failed.AddRange(jobs.Skip(start).Select(j => new KeyValuePair<JobRecord, string>(j, Strings.ResumeRefused)));
                    break;
                }

                var batch = jobs.Skip(start).Take(_batchSize).ToList();
                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = false },
                    Requests = new OrganizationRequestCollection()
                };
                request.Requests.AddRange(batch.Select(job => new ExecuteWorkflowRequest
                {
                    WorkflowId = definitions.TryGetValue(job.ProcessId.Value, out var definition) ? definition : job.ProcessId.Value,
                    EntityId = job.Regarding.Id
                }));

                try
                {
                    var response = (ExecuteMultipleResponse)_service.Execute(request);
                    var faults = response.Responses.Where(r => r.Fault != null).ToDictionary(r => r.RequestIndex, r => r.Fault.Message);
                    for (var i = 0; i < batch.Count; i++)
                    {
                        if (faults.TryGetValue(i, out var message)) result.Failed.Add(new KeyValuePair<JobRecord, string>(batch[i], Strings.RerunFailed(message)));
                        else result.Rerun.Add(batch[i]);
                    }
                }
                catch (Exception error)
                {
                    var message = Strings.RerunFailed(FetchHelper.ErrorMessage(error));
                    result.Failed.AddRange(batch.Select(job => new KeyValuePair<JobRecord, string>(job, message)));
                }

                worker?.ReportProgress(Math.Min(100, (start + batch.Count) * 100 / jobs.Count), Strings.Rerunning(start + batch.Count, jobs.Count));
            }
        }

        /// <summary>workflowactivationid points to the activation: its parent is the workflow definition to execute.</summary>
        private Dictionary<Guid, Guid> GetWorkflowDefinitions(List<Guid> activationIds)
        {
            var definitions = new Dictionary<Guid, Guid>();
            foreach (var chunk in Chunks(activationIds, 200))
            {
                try
                {
                    var query = new QueryExpression("workflow") { ColumnSet = new ColumnSet("parentworkflowid") };
                    query.Criteria.AddCondition("workflowid", ConditionOperator.In, chunk.Cast<object>().ToArray());
                    foreach (var entity in _service.RetrieveMultiple(query).Entities)
                    {
                        var parent = FetchHelper.Get<EntityReference>(entity, "parentworkflowid");
                        if (parent != null) definitions[entity.Id] = parent.Id;
                    }
                }
                catch
                {
                    // Not readable: the activation id itself is used.
                }
            }
            return definitions;
        }

        private static IEnumerable<List<T>> Chunks<T>(List<T> items, int size)
        {
            for (var i = 0; i < items.Count; i += size) yield return items.Skip(i).Take(size).ToList();
        }

        private static OrganizationRequest BuildRequest(JobActionPlan plan, JobRecord job)
        {
            if (plan.Kind == JobActionKind.Delete)
            {
                return new DeleteRequest { Target = new EntityReference(JobQueryService.EntityName, job.Id) };
            }

            var target = new Entity(JobQueryService.EntityName, job.Id);
            switch (plan.Kind)
            {
                case JobActionKind.Resume:
                    target["statecode"] = new OptionSetValue(JobStatus.StateReady);
                    target["statuscode"] = new OptionSetValue(JobStatus.WaitingForResources);
                    break;

                case JobActionKind.Cancel:
                    target["statecode"] = new OptionSetValue(JobStatus.StateCompleted);
                    target["statuscode"] = new OptionSetValue(JobStatus.Canceled);
                    break;

                case JobActionKind.Postpone:
                    target["postponeuntil"] = plan.PostponeUntilUtc;
                    // A suspended job would stay suspended: it is put back in the queue, to run at the new date.
                    if (job.StateCode == JobStatus.StateSuspended)
                    {
                        target["statecode"] = new OptionSetValue(JobStatus.StateReady);
                        target["statuscode"] = new OptionSetValue(JobStatus.WaitingForResources);
                    }
                    break;
            }
            return new UpdateRequest { Target = target };
        }

        /// <summary>Mirrors a successful action on the loaded job, so the screens refresh without reloading.</summary>
        private static void Apply(JobActionPlan plan, JobRecord job)
        {
            switch (plan.Kind)
            {
                case JobActionKind.Resume:
                    job.StateCode = JobStatus.StateReady;
                    job.StatusCode = JobStatus.WaitingForResources;
                    break;
                case JobActionKind.Cancel:
                    job.StateCode = JobStatus.StateCompleted;
                    job.StatusCode = JobStatus.Canceled;
                    job.CompletedOn = DateTime.UtcNow;
                    break;
                case JobActionKind.Postpone:
                    job.PostponeUntil = plan.PostponeUntilUtc;
                    if (job.StateCode == JobStatus.StateSuspended)
                    {
                        job.StateCode = JobStatus.StateReady;
                        job.StatusCode = JobStatus.WaitingForResources;
                    }
                    break;
            }
        }
    }
}
