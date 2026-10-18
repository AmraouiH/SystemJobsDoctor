using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Windows.Forms;
using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using SystemJobsDoctor.Forms;
using SystemJobsDoctor.Helpers;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;
using SystemJobsDoctor.Services;
using SystemJobsDoctor.UI;
using XrmToolBox.Extensibility;

namespace SystemJobsDoctor
{
    /// <summary>
    /// Tool UI: scope, dashboard, error groups, job list with bulk actions, storage cleanup.
    /// Business logic lives in the Services folder; this class only orchestrates and displays.
    /// </summary>
    public partial class SystemJobsDoctorControl : PluginControlBase
    {
        private const string LinkedInUrl = "https://www.linkedin.com/in/hamza-amraoui/";
        private const string DonateUrl = "https://www.paypal.com/paypalme/EntityFieldsAnalyser";

        private const int PageDashboard = 0;
        private const int PageErrors = 1;
        private const int PageJobs = 2;
        private const int PageStorage = 3;

        /// <summary>Statuses selected by the « Problems only » link.</summary>
        private static readonly int[] ProblemStatuses = { JobStatus.WaitingForResources, JobStatus.Waiting, JobStatus.Failed };

        private static readonly string[] Recurrences = { string.Empty, "FREQ=DAILY;INTERVAL=1;", "FREQ=WEEKLY;INTERVAL=1;", "FREQ=MONTHLY;INTERVAL=1;" };

        private sealed class QuickFilter
        {
            public string Label;
            public Func<JobRecord, bool> Predicate;
        }

        /// <summary>Item of the job list table filter (Key null = every table, empty = jobs without regarding record).</summary>
        private sealed class JobTableItem
        {
            public string Key;
            public string Text;
            public override string ToString() => Text;
        }

        private const int PeriodCustom = 6;
        private const int PeriodAll = 5;

        /// <summary>Tables of the organization, loaded with the first analysis of a connection.</summary>
        private List<TableInfo> _tables;
        private bool _suppressPeriod;

        private readonly ThemeRenderer _renderer = new ThemeRenderer();
        private PluginSettings _settings = new PluginSettings();
        private AnalysisResult _result;
        private List<JobRecord> _visibleJobs = new List<JobRecord>();
        private QuickFilter _quickFilter;
        private string _sortColumn = "colJobCreated";
        private bool _sortAscending;
        private bool _controlLoaded;
        private bool _suppressEvents;
        private long? _snapshotCount;
        private StatusCounts _tableCounts;
        private StatusCounts _cleanupCounts;
        private Guid? _createdBulkJobId;
        private readonly List<CheckBox> _statusChecks = new List<CheckBox>();
        private readonly Timer _searchTimer = new Timer { Interval = 300 };

        /// <summary>Background task shown by the loading overlay.</summary>
        private sealed class BusyTask
        {
            public System.ComponentModel.BackgroundWorker Worker;
            public bool Abandoned;
        }

        /// <summary>Delay after Stop before a task stuck in a server call is abandoned (its result will be ignored).</summary>
        private const int AbandonDelayMs = 3000;

        private BusyTask _busy;

        public SystemJobsDoctorControl()
        {
            BuildLayout();
            overlay.StopRequested += (s, e) => StopBusyTask();
            foreach (var row in scopeRows) row.SizeChanged += (s, e) => UpdateScopeHeight();
            tilesRow.SizeChanged += (s, e) => LayoutTiles();
            tsHeader.SizeChanged += (s, e) => AdjustHeader();
            ApplyTheme();
            WireEvents();
            ApplyLanguage();
            ShowPage(PageDashboard);
            Strings.LanguageChanged += Strings_LanguageChanged;
        }

        #region Theme / language

        private void ApplyTheme()
        {
            tsHeader.Renderer = new FluentToolStripRenderer();
            tsddLanguage.DropDown.Renderer = _renderer;
            cmsJobs.Renderer = _renderer;

            // Same glyphs and colors as the Entity Fields Analyser header
            tsbClose.Image = Fluent.Icon(Fluent.Glyph.Close, Fluent.TextSecondary);
            tsbAnalyze.Image = Fluent.Icon(Fluent.Glyph.Play, Fluent.TextOnBrand);
            tsbSummary.Image = Fluent.Icon(Fluent.Glyph.Document, Fluent.Brand);
            tsbExport.Image = Fluent.Icon(Fluent.Glyph.Export, Fluent.ExcelGreen);
            tsbOptions.Image = Fluent.Icon(Fluent.Glyph.Settings, Fluent.TextSecondary);
            tsddLanguage.Image = Fluent.Icon(Fluent.Glyph.Globe, Fluent.TextSecondary);
            tsbAuthor.Image = Fluent.Icon(Fluent.Glyph.Contact, Fluent.TextSecondary);
            tsbHelp.Image = Fluent.Icon(Fluent.Glyph.Help, Fluent.TextSecondary);
            tsbDonate.Image = Fluent.Icon(Fluent.Glyph.Heart, Fluent.HeartRed);

            tsmiResume.Image = LineIcons.ToBitmap(IconKind.Play, 16, Theme.Success);
            tsmiCancel.Image = LineIcons.ToBitmap(IconKind.Stop, 16, Theme.Muted);
            tsmiPostpone.Image = LineIcons.ToBitmap(IconKind.Clock, 16, Theme.Muted);
            tsmiDelete.Image = LineIcons.ToBitmap(IconKind.Trash, 16, Theme.Danger);
            tsmiOpenRecord.Image = LineIcons.ToBitmap(IconKind.Link, 16, Theme.Muted);
            tsmiOpenJob.Image = LineIcons.ToBitmap(IconKind.Link, 16, Theme.Muted);
            tsmiOpenProcess.Image = LineIcons.ToBitmap(IconKind.Link, 16, Theme.Muted);
            tsmiTraceLogs.Image = LineIcons.ToBitmap(IconKind.Log, 16, Theme.Accent);
            tsmiSummarize.Image = LineIcons.ToBitmap(IconKind.Alert, 16, Theme.Accent);
            tsmiSameError.Image = LineIcons.ToBitmap(IconKind.Filter, 16, Theme.Muted);
            tsmiSameProcess.Image = LineIcons.ToBitmap(IconKind.Filter, 16, Theme.Muted);
            tsmiCopyError.Image = LineIcons.ToBitmap(IconKind.Copy, 16, Theme.Muted);
            tsmiCopyIds.Image = LineIcons.ToBitmap(IconKind.Copy, 16, Theme.Muted);

            foreach (var status in JobStatus.All)
            {
                var check = new CheckBox
                {
                    AutoSize = true,
                    Tag = status,
                    ForeColor = Theme.Blend(JobStatus.ColorOf(status), Theme.Text, 0.25f),
                    Font = Theme.Semibold,
                    Image = Theme.CreateDot(JobStatus.ColorOf(status), 14),
                    ImageAlign = ContentAlignment.MiddleLeft,
                    TextImageRelation = TextImageRelation.ImageBeforeText,
                    Margin = new Padding(0, 6, 6, 0)
                };
                _statusChecks.Add(check);
                flpStatuses.Controls.Add(check);
            }

            dgvJobs.Columns["colJobCreated"].HeaderCell.SortGlyphDirection = SortOrder.Descending;
        }

        private void Strings_LanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            ApplyLanguage();
        }

        /// <summary>
        /// Applies the current language to every static text, then refreshes the displayed result.
        /// </summary>
        private void ApplyLanguage()
        {
            _suppressEvents = true;
            try
            {
                overlay.StopText = Strings.Stop;
                overlay.StoppingText = Strings.Stopping;
                overlay.ElapsedFormat = Strings.ElapsedFormat;
                overlay.LongRunningText = Strings.LongRunning;
                tsbClose.Text = Strings.Close;
                tsbClose.ToolTipText = Strings.CloseTip;
                tsbAnalyze.Text = Strings.Analyze;
                tsbAnalyze.ToolTipText = Strings.AnalyzeTip;
                tsbExport.Text = Strings.ExportExcel;
                tsbSummary.Text = Strings.SummaryShort;
                tsbSummary.ToolTipText = Strings.FailedSummaryTip;
                btnJobSummary.Text = Strings.SummaryShort;
                tsbExport.ToolTipText = Strings.ExportTip;
                tsbOptions.Text = Strings.Options;
                tsddLanguage.Text = Strings.IsFrench ? Strings.FrenchName : Strings.EnglishName;
                tsddLanguage.ToolTipText = Strings.LanguageMenu;
                tsmiEnglish.Checked = !Strings.IsFrench;
                tsmiFrench.Checked = Strings.IsFrench;
                tsbAuthor.Text = Strings.AuthorName;
                tsbAuthor.ToolTipText = Strings.AuthorTip;
                tsbDonate.Text = Strings.Donate;
                tsbDonate.ToolTipText = Strings.DonateTip;
                tsbHelp.Text = Strings.Help;
                tsbHelp.ToolTipText = Strings.HelpTip;

                cardScope.Title = Strings.ScopeTitle;
                cardScope.Subtitle = Strings.ScopeSubtitle;
                lblPeriod.Text = Strings.PeriodLabel;
                lblFrom.Text = Strings.FromLabel;
                lblTo.Text = Strings.ToLabel;
                lblDateField.Text = Strings.DateFieldLabel;
                lblPeriodHint.Text = Strings.PeriodHint;
                Refill(cboDateField, Strings.DateFields);
                lblType.Text = Strings.TypeLabel;
                lblTable.Text = Strings.TableLabel;
                lblName.Text = Strings.NameLabel;
                lblMax.Text = Strings.MaxLabel;
                lblStatuses.Text = Strings.StatusesLabel;
                btnAnalyze.Text = Strings.Analyze;
                lnkProblems.Text = Strings.ProblemsOnly;
                lnkAllStatuses.Text = Strings.AllStatuses;
                Theme.SetCue(cboTable, Strings.TableCue);
                Theme.SetCue(txtName, Strings.NameCue);
                Refill(cboPeriod, Strings.Periods);
                Refill(cboType, Strings.JobTypes);
                foreach (var check in _statusChecks) check.Text = JobStatus.Label((int)check.Tag);

                tabs.Tabs[PageDashboard].Text = Strings.TabDashboard;
                tabs.Tabs[PageErrors].Text = Strings.TabErrors;
                tabs.Tabs[PageJobs].Text = Strings.TabJobs;
                tabs.Tabs[PageStorage].Text = Strings.TabStorage;
                tabs.Invalidate();

                // Dashboard
                tileScope.Caption = Strings.TileScope;
                tileWaiting.Caption = Strings.StatusWaitingForResources;
                tileSuspended.Caption = Strings.StatusSuspended;
                tileRunning.Caption = Strings.TileRunning;
                tileFailed.Caption = Strings.StatusFailed;
                tileCanceled.Caption = Strings.StatusCanceled;
                tileSucceeded.Caption = Strings.StatusSucceeded;
                cardTimeline.Title = Strings.TimelineTitle;
                cardTimeline.Subtitle = Strings.TimelineSubtitle;
                chartTimeline.SeriesList[0].Label = Strings.StatusFailed;
                chartTimeline.SeriesList[1].Label = Strings.StatusSuspended;
                chartTimeline.SeriesList[2].Label = Strings.SeriesWaiting;
                chartTimeline.SeriesList[3].Label = Strings.StatusCanceled;
                chartTimeline.SeriesList[4].Label = Strings.StatusSucceeded;
                chartTimeline.EmptyText = Strings.NoData;
                chartTimeline.Invalidate();
                cardByStatus.Title = Strings.ByStatusTitle;
                cardByType.Title = Strings.ByTypeTitle;
                cardByTable.Title = Strings.ByTableTitle;
                cardByProcess.Title = Strings.ByProcessTitle;
                foreach (var card in new[] { cardByStatus, cardByType, cardByTable, cardByProcess }) card.Subtitle = Strings.ClickToFilter;
                foreach (var bar in new[] { barStatus, barType, barTable, barProcess, barTopFailing }) bar.EmptyText = Strings.NoData;
                barTableStatuses.EmptyText = Strings.CountAllHint;

                // Errors
                cardGroups.Title = Strings.GroupsTitle;
                cardTopFailing.Title = Strings.TopFailingTitle;
                cardTopFailing.Subtitle = Strings.TopFailingSubtitle;
                cardGroupDetail.Title = Strings.GroupDetailTitle;
                cardGroupDetail.Subtitle = Strings.GroupDetailSubtitle;
                SetHeaders(dgvGroups, Strings.ColCount, Strings.StatusFailed, Strings.StatusSuspended, Strings.ColSignature, Strings.ColTopProcess,
                    Strings.ColProcesses, Strings.ColTables, Strings.ColLastSeen);
                btnGroupJobs.Text = Strings.ShowJobs;
                btnGroupResume.Text = Strings.Resume;
                btnGroupCancel.Text = Strings.Cancel;
                btnGroupDelete.Text = Strings.Delete;
                toolTip.SetToolTip(btnGroupResume, Strings.ResumeAll);
                toolTip.SetToolTip(btnGroupCancel, Strings.CancelAll);
                toolTip.SetToolTip(btnGroupDelete, Strings.DeleteAll);
                btnGroupCopy.Text = string.Empty;
                toolTip.SetToolTip(btnGroupCopy, Strings.CopyMessage);

                // Jobs
                cardJobs.Title = Strings.JobsTitle;
                cardJobs.Subtitle = Strings.JobsSubtitle;
                cardJobDetail.Title = Strings.JobDetailTitle;
                cardJobDetail.Subtitle = Strings.JobDetailSubtitle;
                Theme.SetCue(txtJobSearch, Strings.SearchJobsCue);
                FillJobTables();
                SetHeaders(dgvJobs, Strings.ColStatus, Strings.ColName, Strings.ColType, Strings.ColTable, Strings.ColRecord, Strings.ColOwner,
                    Strings.ColCreated, Strings.ColCompleted, Strings.ColPostponeUntil, Strings.ColRetries, Strings.ColError);
                btnOpenRecord.Text = Strings.OpenRecord;
                btnOpenJob.Text = Strings.OpenJob;
                btnOpenProcess.Text = Strings.OpenProcess;
                btnTraceLogs.Text = Strings.TraceLogs;
                btnSummarizeJob.Text = Strings.SummarizeJob;
                toolTip.SetToolTip(btnSummarizeJob, Strings.SummarizeJobTip);
                btnCopyDetails.Text = string.Empty;
                toolTip.SetToolTip(btnCopyDetails, Strings.CopyDetails);
                tsmiResume.Text = Strings.Resume;
                tsmiCancel.Text = Strings.Cancel;
                tsmiPostpone.Text = Strings.Postpone + "…";
                tsmiDelete.Text = Strings.Delete;
                tsmiOpenRecord.Text = Strings.OpenRecord;
                tsmiOpenJob.Text = Strings.OpenJob;
                tsmiOpenProcess.Text = Strings.OpenProcess;
                tsmiTraceLogs.Text = Strings.TraceLogs;
                tsmiSameError.Text = Strings.SameError;
                tsmiSameProcess.Text = Strings.SameProcess;
                tsmiCopyError.Text = Strings.CopyError;
                tsmiCopyIds.Text = Strings.CopyIds;

                // Storage
                cardTableSize.Title = Strings.TableSizeTitle;
                cardTableSize.Subtitle = Strings.TableSizeSubtitle;
                cardTableStatuses.Title = Strings.TableStatusesTitle;
                cardTableStatuses.Subtitle = Strings.TableStatusesSubtitle;
                tileSnapshot.Caption = Strings.TileSnapshot;
                tileTableSize.Caption = Strings.TileTableSize;
                lblAvgKb.Text = Strings.AvgKbLabel;
                lblAdminMb.Text = Strings.AdminMbLabel;
                btnRefreshStorage.Text = Strings.Refresh;
                btnCountAll.Text = Strings.CountAll;
                lblStorageNote.Text = Strings.StorageNote;
                cardRule.Title = Strings.RuleTitle;
                cardRule.Subtitle = Strings.RuleSubtitle;
                lblCleanStatuses.Text = Strings.DeleteJobsLabel;
                chkCleanSucceeded.Text = Strings.StatusSucceeded;
                chkCleanCanceled.Text = Strings.StatusCanceled;
                chkCleanFailed.Text = Strings.StatusFailed;
                lblOlderThan.Text = Strings.OlderThanLabel;
                lblCleanType.Text = Strings.TypeLabel;
                Refill(cboCleanType, Strings.JobTypes);
                btnEstimate.Text = Strings.Estimate;
                btnPreviewQuery.Text = Strings.PreviewQuery;
                tileCleanCount.Caption = Strings.TileCleanCount;
                tileCleanSpace.Caption = Strings.TileCleanSpace;
                lblRuleNote.Text = Strings.RuleNote;
                cardBulk.Title = Strings.BulkTitle;
                cardBulk.Subtitle = Strings.BulkSubtitle;
                lblJobName.Text = Strings.JobNameLabel;
                lblStart.Text = Strings.StartLabel;
                lblRecurrence.Text = Strings.RecurrenceLabel;
                Refill(cboRecurrence, Strings.RecurrenceOptions);
                btnCreateBulk.Text = Strings.CreateBulk;
                // The default job name follows the language, a name typed by the user is kept.
                if (txtJobName.TextLength == 0 || txtJobName.Text.StartsWith(Strings.AppTitle + " - ", StringComparison.Ordinal))
                {
                    txtJobName.Text = Strings.DefaultBulkJobName((int)numOlderThan.Value);
                }
                lnkBulkJob.Text = Strings.OpenCreatedJob;
                lblBulkList.Text = Strings.ExistingBulkJobs;
                SetHeaders(dgvBulkJobs, Strings.JobNameLabel, Strings.ColStatus, Strings.ColRecurring, Strings.ColNextRun, Strings.ColDeleted, Strings.ColFailures, Strings.ColCreated);

                toolTip.SetToolTip(cboTable, Strings.TableTip);
                toolTip.SetToolTip(cboJobTable, Strings.JobTableTip);
                toolTip.SetToolTip(btnJobSummary, Strings.FailedSummaryListTip);
                toolTip.SetToolTip(dtpFrom, Strings.DateTip);
                toolTip.SetToolTip(dtpTo, Strings.DateTip);
                toolTip.SetToolTip(txtName, Strings.NameTip);
                toolTip.SetToolTip(numMax, Strings.MaxTip);
                toolTip.SetToolTip(lnkProblems, Strings.ProblemsTip);
                toolTip.SetToolTip(btnResume, Strings.ResumeTip);
                toolTip.SetToolTip(btnCancel, Strings.CancelTip);
                toolTip.SetToolTip(btnPostpone, Strings.PostponeTip);
                toolTip.SetToolTip(btnDelete, Strings.DeleteTip);
                toolTip.SetToolTip(btnTraceLogs, Strings.TraceLogsTip);
                toolTip.SetToolTip(numAdminMb, Strings.AdminMbTip);
                toolTip.SetToolTip(numAvgKb, Strings.AvgKbTip);
                toolTip.SetToolTip(btnCountAll, Strings.CountAllTip);
                toolTip.SetToolTip(btnPreviewQuery, Strings.PreviewQueryTip);
            }
            finally
            {
                _suppressEvents = false;
            }

            if (_result != null)
            {
                JobAnalyzer.Analyze(_result);
                RefreshResult();
            }
            else
            {
                RefreshEmptyState();
            }
            RefreshStorageView();
            UpdateActionButtons();
            AdjustHeader();
        }

        /// <summary>
        /// Keeps every header item visible (never in the overflow menu): on narrow windows the buttons show their icon only,
        /// starting with the links on the right (their text stays as tooltip).
        /// </summary>
        private void AdjustHeader()
        {
            if (tsHeader.Width <= 0) return;
            var compactable = new ToolStripItem[] { tsbAuthor, tsbHelp, tsbDonate, tsbOptions, tsddLanguage, tsbSummary, tsbExport };

            tsHeader.SuspendLayout();
            foreach (var item in compactable) item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;

            bool Fits() => tsHeader.Items.Cast<ToolStripItem>().Where(i => i.Available)
                               .Sum(i => i.GetPreferredSize(Size.Empty).Width + i.Margin.Horizontal) + tsHeader.Padding.Horizontal + 24 <= tsHeader.Width;

            foreach (var item in compactable)
            {
                if (Fits()) break;
                item.DisplayStyle = ToolStripItemDisplayStyle.Image;
            }
            tsHeader.ResumeLayout(true);
        }

        private static void Refill(ComboBox combo, string[] items)
        {
            var index = combo.SelectedIndex;
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.AddRange(items.Cast<object>().ToArray());
            combo.SelectedIndex = index >= 0 && index < items.Length ? index : 0;
            combo.EndUpdate();
        }

        private static void SetHeaders(DataGridView grid, params string[] headers)
        {
            for (var i = 0; i < headers.Length && i < grid.Columns.Count; i++) grid.Columns[i].HeaderText = headers[i];
        }

        /// <summary>How to use the tool (Help button of the header, like Entity Fields Analyser).</summary>
        private void ShowHelp()
        {
            using (var form = new TextViewerForm(Strings.Help, Strings.HelpSubtitle, Strings.HelpText, true))
            {
                form.ShowDialog(this);
            }
        }

        #endregion

        #region Events

        private void WireEvents()
        {
            Load += SystemJobsDoctorControl_Load;
            OnCloseTool += (s, e) => SaveSettings();

            tsbClose.Click += (s, e) => CloseTool();
            tsbAnalyze.Click += (s, e) => ExecuteMethod(Analyze);
            btnAnalyze.Click += (s, e) => ExecuteMethod(Analyze);
            tsbExport.Click += (s, e) => Export();
            tsbSummary.Click += (s, e) => ShowFailedSummary(false);
            btnJobSummary.Click += (s, e) => ShowFailedSummary(true);
            tsbOptions.Click += (s, e) => ShowOptions();
            tsbAuthor.Click += (s, e) => OpenUrl(LinkedInUrl);
            tsbDonate.Click += (s, e) => OpenUrl(DonateUrl);
            tsbHelp.Click += (s, e) => ShowHelp();
            tsmiEnglish.Click += (s, e) => SetLanguage(Language.English);
            tsmiFrench.Click += (s, e) => SetLanguage(Language.French);

            cboPeriod.SelectedIndexChanged += (s, e) => ApplyPeriodPreset();
            dtpFrom.ValueChanged += (s, e) => OnPeriodDateEdited();
            dtpTo.ValueChanged += (s, e) => OnPeriodDateEdited();
            cboJobTable.SelectedIndexChanged += (s, e) => { if (!_suppressEvents) ApplyJobsFilter(); };
            lnkProblems.LinkClicked += (s, e) => SetStatusChecks(ProblemStatuses);
            lnkAllStatuses.LinkClicked += (s, e) => SetStatusChecks(JobStatus.All);
            // Nothing is loaded while the scope is edited: only the Analyze button queries the server.
            cboTable.KeyDown += ScopeBox_KeyDown;
            txtName.KeyDown += ScopeBox_KeyDown;

            tabs.SelectedIndexChanged += (s, e) => ShowPage(tabs.SelectedIndex);

            // Dashboard drill-down
            barStatus.ItemClicked += (s, item) => ShowJobsFiltered(item.Label, j => j.StatusCode.ToString() == item.Key);
            barType.ItemClicked += (s, item) => ShowJobsFiltered(item.Label, j => j.OperationType.ToString() == item.Key);
            barTable.ItemClicked += (s, item) => ShowJobsOfTable(item.Key);
            barProcess.ItemClicked += (s, item) => ShowJobsFiltered(item.Label, j => j.ProcessKey == item.Key);
            barTopFailing.ItemClicked += (s, item) => ShowJobsFiltered(Strings.ProblemsOf(item.Label), j => j.ProcessKey == item.Key && JobStatus.IsProblem(j));
            tileWaiting.Click += (s, e) => ShowJobsFiltered(Strings.StatusWaitingForResources, j => j.StatusCode == JobStatus.WaitingForResources);
            tileSuspended.Click += (s, e) => ShowJobsFiltered(Strings.StatusSuspended, j => j.StateCode == JobStatus.StateSuspended);
            tileRunning.Click += (s, e) => ShowJobsFiltered(Strings.TileRunning, j => j.StateCode == JobStatus.StateLocked);
            tileFailed.Click += (s, e) => ShowJobsFiltered(Strings.StatusFailed, j => j.StatusCode == JobStatus.Failed);
            tileCanceled.Click += (s, e) => ShowJobsFiltered(Strings.StatusCanceled, j => j.StatusCode == JobStatus.Canceled);
            tileSucceeded.Click += (s, e) => ShowJobsFiltered(Strings.StatusSucceeded, j => j.StatusCode == JobStatus.Succeeded);
            tileScope.Click += (s, e) => ShowJobsFiltered(null, null);

            // Errors
            dgvGroups.SelectionChanged += (s, e) => ShowGroupDetail();
            dgvGroups.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowGroupJobs(); };
            btnGroupJobs.Click += (s, e) => ShowGroupJobs();
            btnGroupResume.Click += (s, e) => RunGroupAction(JobActionKind.Resume);
            btnGroupCancel.Click += (s, e) => RunGroupAction(JobActionKind.Cancel);
            btnGroupDelete.Click += (s, e) => RunGroupAction(JobActionKind.Delete);
            btnGroupCopy.Click += (s, e) => CopyText(SelectedGroup?.SampleMessage);

            // Jobs
            dgvJobs.CellValueNeeded += DgvJobs_CellValueNeeded;
            dgvJobs.CellPainting += DgvJobs_CellPainting;
            dgvJobs.ColumnHeaderMouseClick += DgvJobs_ColumnHeaderMouseClick;
            dgvJobs.SelectionChanged += (s, e) => { ShowJobDetail(); UpdateActionButtons(); };
            dgvJobs.CellMouseDown += (s, e) =>
            {
                // Right click on an unselected row selects it, like a list view.
                if (e.Button != MouseButtons.Right || e.RowIndex < 0 || dgvJobs.Rows[e.RowIndex].Selected) return;
                dgvJobs.ClearSelection();
                dgvJobs.CurrentCell = dgvJobs.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
            };
            dgvJobs.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OpenRecord(CurrentJob); };
            txtJobSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplyJobsFilter(); };
            btnClearQuickFilter.Click += (s, e) => { _quickFilter = null; ApplyJobsFilter(); };

            btnResume.Click += (s, e) => RunAction(JobActionKind.Resume, SelectedJobs());
            btnCancel.Click += (s, e) => RunAction(JobActionKind.Cancel, SelectedJobs());
            btnPostpone.Click += (s, e) => RunAction(JobActionKind.Postpone, SelectedJobs());
            btnDelete.Click += (s, e) => RunAction(JobActionKind.Delete, SelectedJobs());
            tsmiResume.Click += (s, e) => RunAction(JobActionKind.Resume, SelectedJobs());
            tsmiCancel.Click += (s, e) => RunAction(JobActionKind.Cancel, SelectedJobs());
            tsmiPostpone.Click += (s, e) => RunAction(JobActionKind.Postpone, SelectedJobs());
            tsmiDelete.Click += (s, e) => RunAction(JobActionKind.Delete, SelectedJobs());

            btnOpenRecord.Click += (s, e) => OpenRecord(CurrentJob);
            tsmiOpenRecord.Click += (s, e) => OpenRecord(CurrentJob);
            btnOpenJob.Click += (s, e) => OpenSystemJob(CurrentJob);
            tsmiOpenJob.Click += (s, e) => OpenSystemJob(CurrentJob);
            btnOpenProcess.Click += (s, e) => OpenProcess(CurrentJob);
            tsmiOpenProcess.Click += (s, e) => OpenProcess(CurrentJob);
            btnTraceLogs.Click += (s, e) => ShowTraceLogs(CurrentJob);
            btnSummarizeJob.Click += (s, e) => SummarizeSelection();
            tsmiSummarize.Click += (s, e) => SummarizeSelection();
            tsmiTraceLogs.Click += (s, e) => ShowTraceLogs(CurrentJob);
            btnCopyDetails.Click += (s, e) => CopyText(DescribeJob(CurrentJob));
            tsmiCopyError.Click += (s, e) => CopyText(CurrentJob?.Message ?? CurrentJob?.FriendlyMessage);
            tsmiCopyIds.Click += (s, e) => CopyText(string.Join(Environment.NewLine, SelectedJobs().Select(j => j.Id.ToString())));
            tsmiSameError.Click += (s, e) =>
            {
                var signature = CurrentJob?.Signature;
                if (signature != null) ShowJobsFiltered(Strings.SameErrorFilter, j => j.Signature == signature);
            };
            tsmiSameProcess.Click += (s, e) =>
            {
                var job = CurrentJob;
                if (job != null) ShowJobsFiltered(job.ProcessName, j => j.ProcessKey == job.ProcessKey);
            };
            cmsJobs.Opening += (s, e) => UpdateContextMenu(e);

            // Storage
            btnRefreshStorage.Click += (s, e) => ExecuteMethod(RefreshStorage);
            btnCountAll.Click += (s, e) => ExecuteMethod(CountWholeTable);
            btnEstimate.Click += (s, e) => ExecuteMethod(EstimateCleanup);
            btnPreviewQuery.Click += (s, e) => ExecuteMethod(PreviewCleanupQuery);
            btnCreateBulk.Click += (s, e) => ExecuteMethod(CreateBulkDelete);
            lnkBulkJob.LinkClicked += (s, e) =>
            {
                if (_createdBulkJobId.HasValue) OpenUrl(UrlHelper.Record(OrganizationUrl, JobQueryService.EntityName, _createdBulkJobId.Value));
            };
            dgvBulkJobs.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && dgvBulkJobs.Rows[e.RowIndex].Tag is BulkDeleteJobInfo info)
                {
                    OpenUrl(UrlHelper.Record(OrganizationUrl, "bulkdeleteoperation", info.Id));
                }
            };
            numAvgKb.ValueChanged += (s, e) => { if (!_suppressEvents) RefreshStorageView(); };
            numAdminMb.ValueChanged += (s, e) => { if (!_suppressEvents) RefreshStorageView(); };
            numOlderThan.ValueChanged += (s, e) => { if (!_suppressEvents) OnRuleChanged(); };
            chkCleanSucceeded.CheckedChanged += (s, e) => { if (!_suppressEvents) OnRuleChanged(); };
            chkCleanCanceled.CheckedChanged += (s, e) => { if (!_suppressEvents) OnRuleChanged(); };
            chkCleanFailed.CheckedChanged += (s, e) => { if (!_suppressEvents) OnRuleChanged(); };
            cboCleanType.SelectedIndexChanged += (s, e) => { if (!_suppressEvents) OnRuleChanged(); };
        }

        private void ScopeBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter only validates the field (it also accepts the table suggested by the auto-completion): no analysis.
            if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true;
        }

        #endregion

        #region Lifecycle / connection

        private void SystemJobsDoctorControl_Load(object sender, EventArgs e)
        {
            if (!SettingsManager.Instance.TryLoad(GetType(), out _settings) || _settings == null)
            {
                _settings = new PluginSettings();
                LogInfo("Default settings created.");
            }
            _settings.Statuses = _settings.Statuses ?? new List<int>();

            Strings.Current = Strings.FromCode(_settings.Language);
            ApplyLanguage();

            _suppressEvents = true;
            cboPeriod.SelectedIndex = Clamp(_settings.PeriodIndex, cboPeriod.Items.Count);
            cboDateField.SelectedIndex = Clamp(_settings.DateFieldIndex, cboDateField.Items.Count);
            cboType.SelectedIndex = Clamp(_settings.TypeIndex, cboType.Items.Count);
            numMax.Value = Math.Max(numMax.Minimum, Math.Min(numMax.Maximum, _settings.MaxRecords));
            numAvgKb.Value = Math.Max(numAvgKb.Minimum, Math.Min(numAvgKb.Maximum, _settings.AverageJobSizeKb));
            numOlderThan.Value = Math.Max(numOlderThan.Minimum, Math.Min(numOlderThan.Maximum, _settings.CleanupOlderThanDays));
            chkCleanSucceeded.Checked = _settings.CleanupSucceeded;
            chkCleanCanceled.Checked = _settings.CleanupCanceled;
            chkCleanFailed.Checked = _settings.CleanupFailed;
            cboCleanType.SelectedIndex = 0;
            cboRecurrence.SelectedIndex = 0;
            dtpStart.Value = DefaultStart();
            txtJobName.Text = Strings.DefaultBulkJobName((int)numOlderThan.Value);
            _suppressEvents = false;
            SetStatusChecks(_settings.Statuses.Count > 0 ? _settings.Statuses.ToArray() : ProblemStatuses);
            ApplyPeriodPreset();
            if (cboPeriod.SelectedIndex == PeriodCustom) RestoreCustomPeriod();

            _controlLoaded = true;
            UpdateActionButtons();

            // No automatic analysis: the user sets the scope, then clicks Analyze (which asks for a connection if needed).
        }

        private static int Clamp(int index, int count) => index >= 0 && index < count ? index : 0;

        private static void SetSplitter(SplitContainer split, int distance)
        {
            try
            {
                var size = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
                if (size <= split.Panel1MinSize + split.Panel2MinSize + split.SplitterWidth) return;
                split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(distance, size - split.Panel2MinSize - split.SplitterWidth));
            }
            catch (InvalidOperationException)
            {
                // Container not laid out yet: keep the default distance.
            }
        }

        private static DateTime DefaultStart()
        {
            var tonight = DateTime.Today.AddHours(23);
            return tonight > DateTime.Now.AddMinutes(10) ? tonight : tonight.AddDays(1);
        }

        public override void UpdateConnection(IOrganizationService newService, ConnectionDetail detail, string actionName, object parameter)
        {
            _result = null;
            _quickFilter = null;
            _snapshotCount = null;
            _tableCounts = null;
            _cleanupCounts = null;
            _createdBulkJobId = null;
            _tables = null;
            cboTable.Items.Clear();
            lnkBulkJob.Visible = false;
            lblBulkResult.Text = string.Empty;
            dgvBulkJobs.Rows.Clear();
            RefreshEmptyState();
            RefreshStorageView();

            // When actionName is set (connection requested by ExecuteMethod), the base class replays the action.
            base.UpdateConnection(newService, detail, actionName, parameter);

            if (detail != null) LogInfo("Connection: {0}", detail.WebApplicationUrl);
        }

        private string OrganizationUrl => ConnectionDetail?.WebApplicationUrl;

        private void SetLanguage(Language language)
        {
            _settings.Language = Strings.ToCode(language);
            Strings.Current = language;
            SaveSettings();
        }

        private void ShowOptions()
        {
            using (var form = new OptionsForm(_settings))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }
            _suppressEvents = true;
            numMax.Value = Math.Max(numMax.Minimum, Math.Min(numMax.Maximum, _settings.MaxRecords));
            numAvgKb.Value = Math.Max(numAvgKb.Minimum, Math.Min(numAvgKb.Maximum, _settings.AverageJobSizeKb));
            _suppressEvents = false;
            RefreshStorageView();
            SaveSettings();
        }

        private void SaveSettings()
        {
            if (_settings == null || !_controlLoaded) return;
            _settings.PeriodIndex = cboPeriod.SelectedIndex;
            _settings.DateFieldIndex = cboDateField.SelectedIndex;
            _settings.CustomFrom = dtpFrom.Checked ? dtpFrom.Value : (DateTime?)null;
            _settings.CustomTo = dtpTo.Checked ? dtpTo.Value : (DateTime?)null;
            _settings.TypeIndex = cboType.SelectedIndex;
            _settings.MaxRecords = (int)numMax.Value;
            _settings.AverageJobSizeKb = (int)numAvgKb.Value;
            _settings.Statuses = CheckedStatuses();
            _settings.CleanupOlderThanDays = (int)numOlderThan.Value;
            _settings.CleanupSucceeded = chkCleanSucceeded.Checked;
            _settings.CleanupCanceled = chkCleanCanceled.Checked;
            _settings.CleanupFailed = chkCleanFailed.Checked;
            SettingsManager.Instance.Save(GetType(), _settings);
        }

        #endregion

        #region Scope

        /// <summary>A preset (last 7 days…) fills the start and end dates; Custom keeps them as they are.</summary>
        private void ApplyPeriodPreset()
        {
            if (_suppressEvents || cboPeriod.SelectedIndex == PeriodCustom) return;

            var from = PresetStart(cboPeriod.SelectedIndex, DateTime.Now);
            _suppressPeriod = true;
            try
            {
                if (from.HasValue) dtpFrom.Value = from.Value;
                dtpFrom.Checked = from.HasValue;
                dtpTo.Value = DateTime.Now;
                dtpTo.Checked = false;
            }
            finally
            {
                _suppressPeriod = false;
            }
        }

        /// <summary>Saved custom dates; without them, the last 7 days (the dates would otherwise both be « now »).</summary>
        private void RestoreCustomPeriod()
        {
            _suppressPeriod = true;
            try
            {
                var from = _settings.CustomFrom;
                var to = _settings.CustomTo;
                if (!from.HasValue && !to.HasValue) from = DateTime.Now.AddDays(-7);
                dtpFrom.Value = from ?? DateTime.Now.AddDays(-7);
                dtpFrom.Checked = from.HasValue;
                dtpTo.Value = to ?? DateTime.Now;
                dtpTo.Checked = to.HasValue;
            }
            finally
            {
                _suppressPeriod = false;
            }
        }

        /// <summary>Editing a date switches the period to Custom.</summary>
        private void OnPeriodDateEdited()
        {
            if (_suppressPeriod || _suppressEvents || cboPeriod.SelectedIndex == PeriodCustom) return;
            _suppressPeriod = true;
            try
            {
                cboPeriod.SelectedIndex = PeriodCustom;
            }
            finally
            {
                _suppressPeriod = false;
            }
        }

        private static DateTime? PresetStart(int index, DateTime now)
        {
            switch (index)
            {
                case 0: return now.AddHours(-24);
                case 1: return now.AddDays(-7);
                case 2: return now.AddDays(-30);
                case 3: return now.AddDays(-90);
                case 4: return now.AddYears(-1);
                default: return null;
            }
        }

        /// <summary>
        /// Logical name of the table filter: the selected table, a "Display name (logical)" text, a display name or a logical name.
        /// </summary>
        private string ResolveScopeTable()
        {
            var text = cboTable.Text.Trim();
            if (text.Length == 0) return string.Empty;
            if (cboTable.SelectedItem is TableInfo selected && selected.ToString() == cboTable.Text) return selected.LogicalName;

            var match = System.Text.RegularExpressions.Regex.Match(text, @"\(([A-Za-z0-9_]+)\)\s*$");
            if (match.Success) return match.Groups[1].Value.ToLowerInvariant();

            var byName = _tables?.FirstOrDefault(t => string.Equals(t.DisplayName, text, StringComparison.CurrentCultureIgnoreCase)
                                                      || string.Equals(t.LogicalName, text, StringComparison.OrdinalIgnoreCase));
            return byName?.LogicalName ?? text.ToLowerInvariant();
        }

        private void FillScopeTables()
        {
            if (_tables == null) return;
            var text = cboTable.Text;
            cboTable.BeginUpdate();
            cboTable.Items.Clear();
            cboTable.Items.AddRange(_tables.Cast<object>().ToArray());
            cboTable.EndUpdate();
            cboTable.Text = text;
        }

        private void SetStatusChecks(IEnumerable<int> statuses)
        {
            var set = new HashSet<int>(statuses);
            foreach (var check in _statusChecks) check.Checked = set.Contains((int)check.Tag);
        }

        private List<int> CheckedStatuses() => _statusChecks.Where(c => c.Checked).Select(c => (int)c.Tag).ToList();

        private JobFilter BuildFilter()
        {
            var filter = new JobFilter
            {
                Statuses = CheckedStatuses(),
                Type = (JobTypeFilter)Math.Max(0, cboType.SelectedIndex),
                Table = ResolveScopeTable(),
                DateAttribute = JobFilter.DateAttributes[Math.Max(0, Math.Min(JobFilter.DateAttributes.Length - 1, cboDateField.SelectedIndex))],
                NameContains = txtName.Text.Trim(),
                MaxRecords = (int)numMax.Value
            };

            if (cboPeriod.SelectedIndex == PeriodCustom)
            {
                if (dtpFrom.Checked) filter.FromUtc = dtpFrom.Value.ToUniversalTime();
                if (dtpTo.Checked) filter.ToUtc = dtpTo.Value.ToUniversalTime();
            }
            else
            {
                // Presets are relative to the time of the analysis.
                filter.FromUtc = PresetStart(cboPeriod.SelectedIndex, DateTime.Now)?.ToUniversalTime();
                ApplyPeriodPreset();
            }
            return filter;
        }

        #endregion

        #region Analysis

        /// <summary>
        /// Loads the jobs of the scope, counts every status on the server and computes the analysis
        /// (public: invoked by ExecuteMethod after connecting).
        /// </summary>
        public void Analyze()
        {
            var filter = BuildFilter();
            if (filter.Statuses.Count == 0)
            {
                MessageBox.Show(this, Strings.NoStatusSelected, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc >= filter.ToUtc)
            {
                MessageBox.Show(this, Strings.InvalidPeriod, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveSettings();
            var knownTables = _tables;
            var parallelism = _settings.ParallelQueries;

            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.LoadingJobs,
                Work = (worker, args) =>
                {
                    var queries = new JobQueryService(Service);
                    var tables = knownTables;
                    if (tables == null)
                    {
                        worker.ReportProgress(0, Strings.ReadingTableNames);
                        try
                        {
                            tables = queries.LoadTables();
                        }
                        catch (Exception error)
                        {
                            LogWarning("Table list unavailable: {0}", error.Message);
                        }
                    }
                    else
                    {
                        queries.Prime(tables);
                    }

                    var result = new AnalysisResult { Filter = filter, Tables = tables };
                    result.Jobs = queries.LoadJobs(filter, m => worker.ReportProgress(0, m), out var truncated, () => worker.CancellationPending);
                    result.Truncated = truncated;
                    result.Stopped = worker.CancellationPending;

                    // Stopped while loading: the jobs already loaded are analyzed, the server counts are skipped.
                    if (!result.Stopped)
                    {
                        worker.ReportProgress(0, Strings.CountingOnServer);
                        try
                        {
                            var counter = new JobCountService(Service, queries) { IsCancelled = () => worker.CancellationPending, Parallelism = parallelism };
                            result.ServerCounts = counter.CountByStatus(filter, (m, p) => worker.ReportProgress(p, m));
                        }
                        catch (OperationCanceledException)
                        {
                            result.Stopped = true;
                        }
                        catch (Exception error)
                        {
                            LogWarning("Server counts unavailable: {0}", error.Message);
                        }
                    }

                    result.TableNames = queries.GetDisplayNames(result.Jobs.Select(j => j.RegardingTable), m => worker.ReportProgress(0, m));
                    worker.ReportProgress(0, Strings.Analyzing);
                    JobAnalyzer.Analyze(result);
                    args.Result = result;
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.AnalysisFailed, e.Error);
                        return;
                    }
                    _result = (AnalysisResult)e.Result;
                    _quickFilter = null;
                    if (_tables == null && _result.Tables != null)
                    {
                        _tables = _result.Tables;
                        FillScopeTables();
                    }
                    LogInfo("Analysis: {0} jobs loaded, {1} error groups.", _result.Jobs.Count, _result.ErrorGroups.Count);
                    RefreshResult();
                }
            });
        }

        private void RefreshEmptyState()
        {
            foreach (var tile in new[] { tileScope, tileWaiting, tileSuspended, tileRunning, tileFailed, tileCanceled, tileSucceeded }) tile.Value = "–";
            lblCountsNote.Text = string.Empty;
            lblResultInfo.Text = Strings.NotAnalyzed;
            chartTimeline.Buckets = null;
            foreach (var bar in new[] { barStatus, barType, barTable, barProcess, barTopFailing }) bar.Items = null;
            dgvGroups.Rows.Clear();
            ShowGroupDetail();
            _visibleJobs = new List<JobRecord>();
            dgvJobs.RowCount = 0;
            lblJobCount.Text = string.Empty;
            ShowJobDetail();
            UpdateBadges();
        }

        /// <summary>Displays the current result on every page.</summary>
        private void RefreshResult()
        {
            if (_result == null)
            {
                RefreshEmptyState();
                return;
            }

            RefreshDashboard();
            RefreshErrors();
            FillJobTables();
            ApplyJobsFilter();
            UpdateBadges();

            var info = Strings.ResultInfo(_result.Jobs.Count, _result.AnalyzedOn);
            if (_result.Stopped) info = Strings.StoppedInfo + " · " + info;
            else if (_result.Truncated) info = Strings.Truncated(_result.Filter.MaxRecords) + " · " + info;
            lblResultInfo.Text = info;
            lblResultInfo.ForeColor = _result.Truncated || _result.Stopped ? Theme.WarningText : Theme.Muted;
            if (_result.Stopped && _result.ServerCounts == null) lblCountsNote.Text = Strings.CountsSkipped;
        }

        private void UpdateBadges()
        {
            var groups = _result?.ErrorGroups.Count ?? 0;
            tabs.Tabs[PageErrors].Badge = groups > 0 ? groups.ToString("N0") : null;
            tabs.Tabs[PageJobs].Badge = _result != null ? _result.Jobs.Count.ToString("N0") : null;
            tabs.Invalidate();
        }

        private void ShowPage(int index)
        {
            var pages = new[] { pageDashboard, pageErrors, pageJobs, pageStorage };
            for (var i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
            if (tabs.SelectedIndex != index) tabs.SelectedIndex = index;

            // Splitters can only be placed once their page is visible (hidden pages are not laid out).
            if (index == PageErrors && !_errorsSplitPlaced) BeginInvoke(new Action(PlaceErrorSplitters));
            if (index == PageJobs && !_jobsSplitPlaced) BeginInvoke(new Action(PlaceJobSplitter));

        }

        /// <summary>The scope card is as tall as its rows (they wrap on narrow screens).</summary>
        private void UpdateScopeHeight()
        {
            var height = cardScope.Padding.Vertical + scopeRows.Sum(r => r.Height);
            if (cardScope.Height != height) cardScope.Height = height;
        }

        /// <summary>The seven tiles share the width (no horizontal scrollbar).</summary>
        private void LayoutTiles()
        {
            var tiles = tilesRow.Controls.Cast<Control>().ToList();
            if (tiles.Count == 0 || tilesRow.ClientSize.Width <= 0) return;
            var gap = tiles[0].Margin.Horizontal;
            var width = Math.Max(110, Math.Min(200, (tilesRow.ClientSize.Width - 2) / tiles.Count - gap));
            foreach (var tile in tiles) tile.Width = width;
        }

        private bool _errorsSplitPlaced;
        private bool _jobsSplitPlaced;

        private void PlaceErrorSplitters()
        {
            if (IsDisposed || !pageErrors.Visible || splitErrors.Width < 600) return;
            // Wide enough for the group buttons on one line (French labels included).
            SetSplitter(splitErrors, splitErrors.Width - Math.Max(570, Math.Min(660, splitErrors.Width * 45 / 100)));
            SetSplitter(splitErrorsRight, splitErrorsRight.Height * 40 / 100);
            splitErrors.Panel2MinSize = 560;
            splitErrorsRight.Panel1MinSize = 110;
            splitErrorsRight.Panel2MinSize = 200;
            _errorsSplitPlaced = true;
        }

        private void PlaceJobSplitter()
        {
            if (IsDisposed || !pageJobs.Visible || splitJobs.Height < 360) return;
            var detail = Math.Max(190, Math.Min(260, splitJobs.Height * 38 / 100));
            SetSplitter(splitJobs, splitJobs.Height - detail);
            splitJobs.Panel1MinSize = 170;
            splitJobs.Panel2MinSize = 180;
            _jobsSplitPlaced = true;
        }

        #endregion

        #region Dashboard

        private void RefreshDashboard()
        {
            var counts = _result.ServerCounts;
            if (counts != null)
            {
                var prefix = counts.Approximate ? "≥ " : string.Empty;
                tileScope.Value = prefix + counts.Total.ToString("N0");
                tileWaiting.Value = counts[JobStatus.WaitingForResources].ToString("N0");
                tileSuspended.Value = counts[JobStatus.Waiting].ToString("N0");
                tileRunning.Value = (counts[JobStatus.InProgress] + counts[JobStatus.Pausing] + counts[JobStatus.Canceling]).ToString("N0");
                tileFailed.Value = counts[JobStatus.Failed].ToString("N0");
                tileCanceled.Value = counts[JobStatus.Canceled].ToString("N0");
                tileSucceeded.Value = counts[JobStatus.Succeeded].ToString("N0");
                lblCountsNote.Text = Strings.CountsNote(counts.QueryCount, counts.Approximate);
            }
            else
            {
                // Server counts unavailable: the tiles show the loaded jobs.
                var jobs = _result.Jobs;
                tileScope.Value = jobs.Count.ToString("N0");
                tileWaiting.Value = jobs.Count(j => j.StatusCode == JobStatus.WaitingForResources).ToString("N0");
                tileSuspended.Value = jobs.Count(j => j.StateCode == JobStatus.StateSuspended).ToString("N0");
                tileRunning.Value = jobs.Count(j => j.StateCode == JobStatus.StateLocked).ToString("N0");
                tileFailed.Value = jobs.Count(j => j.StatusCode == JobStatus.Failed).ToString("N0");
                tileCanceled.Value = jobs.Count(j => j.StatusCode == JobStatus.Canceled).ToString("N0");
                tileSucceeded.Value = jobs.Count(j => j.StatusCode == JobStatus.Succeeded).ToString("N0");
                lblCountsNote.Text = Strings.CountsUnavailable;
            }

            chartTimeline.Buckets = _result.Days;
            cardTimeline.Subtitle = chartTimeline.Weekly ? Strings.TimelineSubtitleWeekly : Strings.TimelineSubtitle;
            barStatus.Items = _result.ByStatus;
            barType.Items = _result.ByType;
            barTable.Items = _result.ByTable;
            barProcess.Items = _result.ByProcess;
        }

        #endregion

        #region Errors

        private ErrorGroup SelectedGroup => dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Tag as ErrorGroup : null;

        private void RefreshErrors()
        {
            var previous = SelectedGroup?.Signature;
            dgvGroups.SuspendLayout();
            dgvGroups.Rows.Clear();
            foreach (var group in _result.ErrorGroups)
            {
                var index = dgvGroups.Rows.Add(group.Count.ToString("N0"), group.FailedCount.ToString("N0"), group.SuspendedCount.ToString("N0"),
                    group.Signature, group.TopProcess, group.ProcessCount, group.Tables, FormatDate(group.LastSeen));
                var row = dgvGroups.Rows[index];
                row.Tag = group;
                row.Cells["colGroupSignature"].ToolTipText = group.SampleMessage;
            }
            dgvGroups.ResumeLayout();

            var problems = _result.Jobs.Count(JobStatus.IsProblem);
            cardGroups.Subtitle = _result.ErrorGroups.Count == 0
                ? Strings.NoErrors
                : Strings.GroupsSubtitle(_result.ErrorGroups.Sum(g => g.Count), _result.ErrorGroups.Count);

            barTopFailing.Items = JobAnalyzer.TopFailing(_result, 10).Select(p => new BreakdownItem
            {
                Key = p.Key,
                Label = $"{p.Name}  ·  {Strings.ProcessKindLabel(p.Kind)}  ·  {Strings.FailureRate(p.FailureRate)}",
                Count = p.Problems,
                Color = Theme.Danger
            }).ToList();

            var reselect = dgvGroups.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => ((ErrorGroup)r.Tag).Signature == previous);
            dgvGroups.ClearSelection();
            if (reselect != null) reselect.Selected = true;
            else if (dgvGroups.Rows.Count > 0) dgvGroups.Rows[0].Selected = true;
            ShowGroupDetail();
            LogInfo("{0} problem jobs in {1} error groups.", problems, _result.ErrorGroups.Count);
        }

        private void ShowGroupDetail()
        {
            var group = SelectedGroup;
            var hasGroup = group != null;
            foreach (var button in new[] { btnGroupJobs, btnGroupResume, btnGroupCancel, btnGroupDelete, btnGroupCopy }) button.Enabled = hasGroup;

            if (!hasGroup)
            {
                txtGroupSample.Text = string.Empty;
                lblGroupInfo.Text = string.Empty;
                return;
            }

            txtGroupSample.Text = (group.SampleMessage ?? string.Empty).Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            lblGroupInfo.Text = Strings.GroupInfo(group);
            btnGroupResume.Enabled = group.Jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Resume, j));
            btnGroupCancel.Enabled = group.Jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Cancel, j));
            btnGroupDelete.Enabled = group.Jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Delete, j));
        }

        private void ShowGroupJobs()
        {
            var group = SelectedGroup;
            if (group == null) return;
            var signature = group.Signature;
            ShowJobsFiltered(Strings.ErrorFilter(signature), j => j.Signature == signature && JobStatus.IsProblem(j));
        }

        private void RunGroupAction(JobActionKind kind)
        {
            var group = SelectedGroup;
            if (group != null) RunAction(kind, group.Jobs.ToList());
        }

        #endregion

        #region Jobs

        private void ShowJobsFiltered(string label, Func<JobRecord, bool> predicate)
        {
            if (_result == null) return;
            _quickFilter = predicate == null ? null : new QuickFilter { Label = label, Predicate = predicate };
            SelectJobTable(null);
            txtJobSearch.Text = string.Empty;
            _searchTimer.Stop();
            ApplyJobsFilter();
            ShowPage(PageJobs);
        }

        private void ApplyJobsFilter()
        {
            if (_result == null)
            {
                _visibleJobs = new List<JobRecord>();
                dgvJobs.RowCount = 0;
                return;
            }

            IEnumerable<JobRecord> jobs = _result.Jobs;
            if (_quickFilter != null) jobs = jobs.Where(_quickFilter.Predicate);
            if (cboJobTable.SelectedItem is JobTableItem table && table.Key != null)
            {
                jobs = jobs.Where(j => string.Equals(j.RegardingTable ?? string.Empty, table.Key, StringComparison.OrdinalIgnoreCase));
            }

            var search = txtJobSearch.Text.Trim();
            if (search.Length > 0)
            {
                jobs = jobs.Where(j => Contains(j.Name, search) || Contains(j.ProcessName, search) || Contains(j.RegardingTable, search)
                                       || Contains(j.Regarding?.Name, search) || Contains(j.OwnerName, search) || Contains(j.ShortError, search)
                                       || Contains(j.OperationTypeLabel, search) || j.Id.ToString().StartsWith(search, StringComparison.OrdinalIgnoreCase));
            }

            _visibleJobs = Sort(jobs).ToList();

            dgvJobs.RowCount = 0;
            dgvJobs.RowCount = _visibleJobs.Count;
            dgvJobs.ClearSelection();
            if (_visibleJobs.Count > 0)
            {
                dgvJobs.CurrentCell = dgvJobs.Rows[0].Cells[0];
                dgvJobs.Rows[0].Selected = true;
            }
            dgvJobs.Invalidate();

            lblQuickFilter.Visible = btnClearQuickFilter.Visible = _quickFilter != null;
            lblQuickFilter.Text = _quickFilter == null ? string.Empty : Shorten(_quickFilter.Label ?? string.Empty, 60);
            lblJobCount.Text = Strings.JobCount(_visibleJobs.Count, _result.Jobs.Count);
            ShowJobDetail();
            UpdateActionButtons();
        }

        private static bool Contains(string value, string search) => value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Shorten(string value, int length) => value.Length <= length ? value : value.Substring(0, length) + "…";

        private IEnumerable<JobRecord> Sort(IEnumerable<JobRecord> jobs)
        {
            Func<JobRecord, IComparable> key;
            switch (_sortColumn)
            {
                case "colJobStatus": key = j => JobStatus.Label(j.StatusCode); break;
                case "colJobName": key = j => j.ProcessName ?? j.Name ?? string.Empty; break;
                case "colJobType": key = j => j.OperationTypeLabel ?? string.Empty; break;
                case "colJobTable": key = j => j.RegardingTable ?? string.Empty; break;
                case "colJobRecord": key = j => j.Regarding?.Name ?? string.Empty; break;
                case "colJobOwner": key = j => j.OwnerName ?? string.Empty; break;
                case "colJobCompleted": key = j => j.CompletedOn ?? DateTime.MinValue; break;
                case "colJobPostpone": key = j => j.PostponeUntil ?? DateTime.MinValue; break;
                case "colJobRetries": key = j => j.RetryCount ?? 0; break;
                case "colJobError": key = j => j.ShortError ?? string.Empty; break;
                default: key = j => j.CreatedOn ?? DateTime.MinValue; break;
            }
            return _sortAscending ? jobs.OrderBy(key) : jobs.OrderByDescending(key);
        }

        private void DgvJobs_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var column = dgvJobs.Columns[e.ColumnIndex];
            if (_sortColumn == column.Name) _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column.Name;
                _sortAscending = column.Name != "colJobCreated" && column.Name != "colJobCompleted" && column.Name != "colJobRetries";
            }
            foreach (DataGridViewColumn other in dgvJobs.Columns) other.HeaderCell.SortGlyphDirection = SortOrder.None;
            column.HeaderCell.SortGlyphDirection = _sortAscending ? SortOrder.Ascending : SortOrder.Descending;
            ApplyJobsFilter();
        }

        private void DgvJobs_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visibleJobs.Count) return;
            var job = _visibleJobs[e.RowIndex];
            switch (dgvJobs.Columns[e.ColumnIndex].Name)
            {
                case "colJobStatus": e.Value = JobStatus.Label(job.StatusCode); break;
                case "colJobName": e.Value = job.ProcessName ?? job.Name; break;
                case "colJobType": e.Value = job.OperationTypeLabel; break;
                case "colJobTable": e.Value = job.RegardingTable; break;
                case "colJobRecord": e.Value = job.Regarding?.Name ?? (job.Regarding != null ? job.Regarding.Id.ToString() : null); break;
                case "colJobOwner": e.Value = job.OwnerName; break;
                case "colJobCreated": e.Value = FormatDate(job.CreatedOn); break;
                case "colJobCompleted": e.Value = FormatDate(job.CompletedOn); break;
                case "colJobPostpone": e.Value = FormatDate(job.PostponeUntil); break;
                case "colJobRetries": e.Value = job.RetryCount; break;
                case "colJobError": e.Value = job.ShortError; break;
            }
        }

        private void DgvJobs_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _visibleJobs.Count || dgvJobs.Columns[e.ColumnIndex].Name != "colJobStatus") return;

            var job = _visibleJobs[e.RowIndex];
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground);
            Theme.PaintDivider(e.Graphics, e.CellBounds, dgvJobs.GridColor);

            var color = JobStatus.ColorOf(job.StatusCode);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, e.CellBounds.X + 10, e.CellBounds.Y + (e.CellBounds.Height - 8) / 2f, 8, 8);
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            var textBounds = new Rectangle(e.CellBounds.X + 24, e.CellBounds.Y, e.CellBounds.Width - 26, e.CellBounds.Height);
            TextRenderer.DrawText(g, JobStatus.Label(job.StatusCode), Theme.Semibold, textBounds, Theme.Blend(color, Theme.Text, 0.3f),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            e.Handled = true;
        }

        private JobRecord CurrentJob
        {
            get
            {
                var index = dgvJobs.CurrentCell?.RowIndex ?? -1;
                return index >= 0 && index < _visibleJobs.Count ? _visibleJobs[index] : null;
            }
        }

        private List<JobRecord> SelectedJobs()
        {
            var count = dgvJobs.Rows.GetRowCount(DataGridViewElementStates.Selected);
            if (count == 0) return CurrentJob != null ? new List<JobRecord> { CurrentJob } : new List<JobRecord>();
            if (count == _visibleJobs.Count) return _visibleJobs.ToList();

            return dgvJobs.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Index)
                .Where(i => i >= 0 && i < _visibleJobs.Count)
                .OrderBy(i => i)
                .Select(i => _visibleJobs[i])
                .ToList();
        }

        private void UpdateActionButtons()
        {
            var jobs = _result == null ? new List<JobRecord>() : SelectedJobs();
            SetActionButton(btnResume, Strings.Resume, JobActionKind.Resume, jobs);
            SetActionButton(btnCancel, Strings.Cancel, JobActionKind.Cancel, jobs);
            SetActionButton(btnPostpone, Strings.Postpone, JobActionKind.Postpone, jobs);
            SetActionButton(btnDelete, Strings.Delete, JobActionKind.Delete, jobs);
            tsbExport.Enabled = _result != null;
            tsbSummary.Enabled = _result != null;
            btnJobSummary.Enabled = _result != null && _visibleJobs.Count > 0;

            var job = CurrentJob;
            btnOpenRecord.Enabled = job?.Regarding != null;
            btnOpenJob.Enabled = job != null;
            btnOpenProcess.Enabled = job?.ProcessId != null && job.ProcessKind != ProcessKind.Other;
            btnTraceLogs.Enabled = job != null && TraceLogService.CanSearch(job);
            btnCopyDetails.Enabled = job != null;
            var selectedCount = _result == null ? 0 : jobs.Count;
            btnSummarizeJob.Enabled = selectedCount > 0;
            btnSummarizeJob.Text = selectedCount > 1 ? Strings.SummarizeJobs(selectedCount) : Strings.SummarizeJob;
        }

        private static void SetActionButton(Control button, string text, JobActionKind kind, List<JobRecord> jobs)
        {
            var eligible = jobs.Count(j => JobActionPlan.IsEligible(kind, j));
            button.Text = eligible > 0 ? $"{text} ({eligible:N0})" : text;
            button.Enabled = eligible > 0;
        }

        private void UpdateContextMenu(System.ComponentModel.CancelEventArgs e)
        {
            var jobs = SelectedJobs();
            if (jobs.Count == 0)
            {
                e.Cancel = true;
                return;
            }
            var job = CurrentJob;
            tsmiSummarize.Text = jobs.Count > 1 ? Strings.SummarizeJobs(jobs.Count) : Strings.SummarizeJob;
            tsmiResume.Enabled = jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Resume, j));
            tsmiCancel.Enabled = jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Cancel, j));
            tsmiPostpone.Enabled = jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Postpone, j));
            tsmiDelete.Enabled = jobs.Any(j => JobActionPlan.IsEligible(JobActionKind.Delete, j));
            tsmiOpenRecord.Enabled = job?.Regarding != null;
            tsmiOpenJob.Enabled = job != null;
            tsmiOpenProcess.Enabled = job?.ProcessId != null && job.ProcessKind != ProcessKind.Other;
            tsmiTraceLogs.Enabled = job != null && TraceLogService.CanSearch(job);
            tsmiSameError.Enabled = job?.Signature != null;
            tsmiSameProcess.Enabled = job != null;
            tsmiCopyError.Enabled = job != null && job.HasError;
        }

        private void ShowJobDetail()
        {
            var job = CurrentJob;
            dgvDetail.Rows.Clear();
            if (job == null)
            {
                txtJobMessage.Text = string.Empty;
                return;
            }

            void Add(string key, object value)
            {
                if (value == null || (value is string text && text.Length == 0)) return;
                dgvDetail.Rows.Add(key, value);
            }

            Add(Strings.ColName, job.Name);
            Add(Strings.ColStatus, JobStatus.Label(job.StatusCode));
            Add(Strings.ColType, job.OperationTypeLabel);
            Add(Strings.DetailProcess, job.ProcessName == job.Name ? null : job.ProcessName);
            Add(Strings.DetailProcessKind, Strings.ProcessKindLabel(job.ProcessKind));
            Add(Strings.DetailMessage, job.MessageName);
            Add(Strings.ColTable, job.RegardingTable);
            Add(Strings.ColRecord, job.Regarding == null ? null : $"{job.Regarding.Name} ({job.Regarding.Id})");
            Add(Strings.ColOwner, job.OwnerName);
            Add(Strings.ColCreated, FormatDate(job.CreatedOn, true));
            Add(Strings.DetailStarted, FormatDate(job.StartedOn, true));
            Add(Strings.ColCompleted, FormatDate(job.CompletedOn, true));
            Add(Strings.DetailDuration, job.Duration.HasValue ? FormatDuration(job.Duration.Value) : null);
            Add(Strings.ColPostponeUntil, FormatDate(job.PostponeUntil, true));
            Add(Strings.ColRetries, job.RetryCount);
            Add(Strings.DetailDepth, job.Depth);
            Add(Strings.DetailErrorCode, job.ErrorCodeHex);
            Add(Strings.DetailCorrelation, job.CorrelationId);
            Add(Strings.DetailJobId, job.Id);

            var message = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(job.FriendlyMessage) && job.FriendlyMessage != job.Message)
            {
                message.AppendLine(job.FriendlyMessage.Trim()).AppendLine().AppendLine(new string('─', 40)).AppendLine();
            }
            message.Append(job.Message ?? (job.HasError ? string.Empty : Strings.NoMessage));
            txtJobMessage.Text = message.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        }

        private string DescribeJob(JobRecord job)
        {
            if (job == null) return null;
            var builder = new StringBuilder();
            foreach (DataGridViewRow row in dgvDetail.Rows) builder.AppendLine($"{row.Cells[0].Value}: {row.Cells[1].Value}");
            builder.AppendLine().Append(txtJobMessage.Text);
            return builder.ToString();
        }

        /// <summary>
        /// Table filter of the job list: every table of the loaded jobs with its count. Useful when the scope has no table:
        /// pick a table and the grid only shows its jobs.
        /// </summary>
        private void FillJobTables()
        {
            var previous = (cboJobTable.SelectedItem as JobTableItem)?.Key;
            _suppressEvents = true;
            try
            {
                cboJobTable.BeginUpdate();
                cboJobTable.Items.Clear();
                var total = _result?.Jobs.Count ?? 0;
                cboJobTable.Items.Add(new JobTableItem { Key = null, Text = Strings.AllTables(total) });
                if (_result != null)
                {
                    foreach (var item in _result.ByTable)
                    {
                        var name = item.Key.Length == 0 ? Strings.NoTable
                            : string.Equals(item.Label, item.Key, StringComparison.OrdinalIgnoreCase) ? item.Label : $"{item.Label} ({item.Key})";
                        cboJobTable.Items.Add(new JobTableItem { Key = item.Key, Text = $"{name} — {item.Count:N0}" });
                    }
                }
                var index = cboJobTable.Items.Cast<JobTableItem>().ToList().FindIndex(i => i.Key == previous);
                cboJobTable.SelectedIndex = Math.Max(0, index);
                cboJobTable.EndUpdate();
                cboJobTable.Enabled = cboJobTable.Items.Count > 2 || (cboJobTable.Items.Count == 2 && index > 0);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void SelectJobTable(string key)
        {
            var index = cboJobTable.Items.Cast<JobTableItem>().ToList().FindIndex(i => i.Key == key);
            _suppressEvents = true;
            try
            {
                cboJobTable.SelectedIndex = Math.Max(0, index);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void ShowJobsOfTable(string key)
        {
            if (_result == null) return;
            _quickFilter = null;
            txtJobSearch.Text = string.Empty;
            _searchTimer.Stop();
            SelectJobTable(key);
            ApplyJobsFilter();
            ShowPage(PageJobs);
        }

        /// <summary>Description of the filters of the job list (heading of the failed jobs summary).</summary>
        private string CurrentListLabel()
        {
            var parts = new List<string>();
            if (cboJobTable.SelectedItem is JobTableItem table && table.Key != null) parts.Add(Strings.TableLabel + " = " + (table.Key.Length == 0 ? Strings.NoTable : table.Key));
            if (_quickFilter?.Label != null) parts.Add(_quickFilter.Label);
            if (txtJobSearch.Text.Trim().Length > 0) parts.Add($"\"{txtJobSearch.Text.Trim()}\"");
            return parts.Count == 0 ? Strings.SummaryScopeAllLabel : Strings.SummaryScopeListLabel(string.Join(" · ", parts));
        }

        private void ShowFailedSummary(bool fromList)
        {
            if (_result == null) return;
            using (var form = new FailedSummaryForm(_result, _visibleJobs.ToList(), Strings.SummaryScopeList(_visibleJobs.Count), CurrentListLabel(), fromList, ConnectionDetail?.ConnectionName))
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// One selected job: detailed summary of that job. Several: summary of the failures of the selection.
        /// </summary>
        private void SummarizeSelection()
        {
            if (_result == null) return;
            var jobs = SelectedJobs();
            if (jobs.Count == 0) return;

            if (jobs.Count == 1)
            {
                var job = jobs[0];
                var text = FailedSummaryService.BuildForJob(job, _result, ConnectionDetail?.ConnectionName, OrganizationUrl);
                using (var form = new TextViewerForm(Strings.JobSummaryTitle, Strings.JobSummarySubtitle(job.ProcessName ?? job.Name), text, true))
                {
                    form.ShowDialog(this);
                }
                return;
            }

            using (var form = new FailedSummaryForm(_result, jobs, Strings.SummaryScopeSelection(jobs.Count), Strings.SummaryScopeSelectionLabel(jobs.Count), true,
                       ConnectionDetail?.ConnectionName))
            {
                form.ShowDialog(this);
            }
        }

        #endregion

        #region Actions

        private void RunAction(JobActionKind kind, List<JobRecord> jobs)
        {
            if (_result == null || jobs == null || jobs.Count == 0) return;

            var plan = JobActionPlan.Build(kind, jobs);
            if (plan.Eligible.Count == 0)
            {
                MessageBox.Show(this, Strings.NothingEligible(kind), Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (kind == JobActionKind.Postpone)
            {
                using (var form = new PostponeForm(plan.Eligible.Count))
                {
                    if (form.ShowDialog(this) != DialogResult.OK) return;
                    plan.PostponeUntilUtc = form.SelectedDate.ToUniversalTime();
                }
            }

            using (var confirm = new ActionConfirmForm(plan))
            {
                if (confirm.ShowDialog(this) != DialogResult.OK) return;
            }

            ExecuteMethod(() => ExecuteAction(plan));
        }

        private void ExecuteAction(JobActionPlan plan)
        {
            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.ActionProgress(plan.Kind, 0, plan.Eligible.Count),
                IsCancelable = true,
                Work = (worker, args) => args.Result = new JobActionService(Service, _settings.BatchSize).Execute(plan, worker),
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.ActionFailed, e.Error);
                        return;
                    }

                    var result = (JobActionResult)e.Result;
                    if (result.Kind == JobActionKind.Delete)
                    {
                        var deleted = new HashSet<Guid>(result.Succeeded.Select(j => j.Id));
                        _result.Jobs.RemoveAll(j => deleted.Contains(j.Id));
                    }

                    LogInfo("{0}: {1} succeeded, {2} failed.", result.Kind, result.Succeeded.Count, result.Failed.Count);
                    foreach (var failure in result.Failed.Take(50)) LogWarning("{0} {1}: {2}", result.Kind, failure.Key.Id, failure.Value);

                    JobAnalyzer.Analyze(_result);
                    var filter = _quickFilter;
                    RefreshResult();
                    if (filter != null)
                    {
                        _quickFilter = filter;
                        ApplyJobsFilter();
                    }
                    lblCountsNote.Text = Strings.CountsStale;

                    MessageBox.Show(this, Strings.ActionSummary(result), Strings.AppTitle, MessageBoxButtons.OK,
                        result.Failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
            });
        }

        #endregion

        #region Links / trace logs

        private void OpenRecord(JobRecord job)
        {
            if (job?.Regarding == null) return;
            OpenUrl(UrlHelper.Record(OrganizationUrl, job.Regarding.LogicalName, job.Regarding.Id));
        }

        private void OpenSystemJob(JobRecord job)
        {
            if (job == null) return;
            OpenUrl(UrlHelper.Record(OrganizationUrl, JobQueryService.EntityName, job.Id));
        }

        private void OpenProcess(JobRecord job)
        {
            if (job?.ProcessId == null) return;
            var entity = job.ProcessKind == ProcessKind.Workflow ? "workflow" : job.ProcessKind == ProcessKind.PluginStep ? "sdkmessageprocessingstep" : null;
            if (entity == null) return;
            OpenUrl(UrlHelper.Record(OrganizationUrl, entity, job.ProcessId.Value));
        }

        private void ShowTraceLogs(JobRecord job)
        {
            if (job == null || !TraceLogService.CanSearch(job)) return;
            ExecuteMethod(() =>
            {
                RunWithOverlay(new WorkAsyncInfo
                {
                    Message = Strings.LoadingTraces,
                    Work = (worker, args) =>
                    {
                        var service = new TraceLogService(Service);
                        args.Result = Tuple.Create(service.GetTraces(job), service.GetTraceSetting());
                    },
                    PostWorkCallBack = e =>
                    {
                        if (e.Error != null)
                        {
                            ShowError(Strings.TracesFailed, e.Error);
                            return;
                        }
                        var data = (Tuple<List<TraceLogEntry>, int?>)e.Result;
                        using (var form = new TraceLogForm(job, data.Item1, data.Item2, UrlHelper.List(OrganizationUrl, "plugintracelog")))
                        {
                            form.ShowDialog(this);
                        }
                    }
                });
            });
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(this, Strings.NoUrl, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                LogError("Unable to open {0}: {1}", url, ex.Message);
            }
        }

        private void CopyText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                LogWarning("Clipboard: {0}", ex.Message);
            }
        }

        #endregion

        #region Storage

        private CleanupRule BuildRule() => new CleanupRule
        {
            Succeeded = chkCleanSucceeded.Checked,
            Canceled = chkCleanCanceled.Checked,
            Failed = chkCleanFailed.Checked,
            OlderThanDays = (int)numOlderThan.Value,
            Type = (JobTypeFilter)Math.Max(0, cboCleanType.SelectedIndex)
        };

        /// <summary>Average size of one job: from the admin center size when given, otherwise the manual estimate.</summary>
        private double AverageKb
        {
            get
            {
                if (numAdminMb.Value > 0 && (_snapshotCount ?? _tableCounts?.Total ?? 0) > 0)
                {
                    var rows = _snapshotCount ?? _tableCounts.Total;
                    return (double)numAdminMb.Value * 1024 / rows;
                }
                return (double)numAvgKb.Value;
            }
        }

        private void OnRuleChanged()
        {
            _cleanupCounts = null;
            txtJobName.Text = Strings.DefaultBulkJobName((int)numOlderThan.Value);
            RefreshStorageView();
        }

        /// <summary>Row count snapshot and existing bulk deletion jobs (public: invoked by ExecuteMethod).</summary>
        public void RefreshStorage()
        {
            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.LoadingStorage,
                Work = (worker, args) =>
                {
                    var cleanup = new CleanupService(Service, new JobCountService(Service, new JobQueryService(Service)));
                    var snapshot = cleanup.GetSnapshotRowCount();
                    List<BulkDeleteJobInfo> jobs;
                    try
                    {
                        jobs = cleanup.GetSystemJobBulkDeletes();
                    }
                    catch (Exception error)
                    {
                        LogWarning("Bulk deletion jobs unavailable: {0}", error.Message);
                        jobs = new List<BulkDeleteJobInfo>();
                    }
                    args.Result = Tuple.Create(snapshot, jobs);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.StorageFailed, e.Error);
                        return;
                    }
                    var data = (Tuple<long?, List<BulkDeleteJobInfo>>)e.Result;
                    _snapshotCount = data.Item1;
                    FillBulkJobs(data.Item2);
                    RefreshStorageView();
                }
            });
        }

        public void CountWholeTable()
        {
            var snapshot = _snapshotCount;
            var parallelism = _settings.ParallelQueries;
            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.CountingOnServer,
                Work = (worker, args) =>
                {
                    var cleanup = new CleanupService(Service, new JobCountService(Service, new JobQueryService(Service))
                    {
                        IsCancelled = () => worker.CancellationPending,
                        Parallelism = parallelism
                    });
                    // The row snapshot (instant) plans the time slices: about 40,000 jobs each, counted in parallel.
                    var expected = snapshot ?? cleanup.GetSnapshotRowCount();
                    args.Result = Tuple.Create(cleanup.CountAll((m, p) => worker.ReportProgress(p, m), expected), expected);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.CountFailed, e.Error);
                        return;
                    }
                    var data = (Tuple<StatusCounts, long?>)e.Result;
                    _tableCounts = data.Item1;
                    if (!_snapshotCount.HasValue) _snapshotCount = data.Item2;
                    LogInfo("Whole table counted: {0} jobs, {1} queries.", _tableCounts.Total, _tableCounts.QueryCount);
                    RefreshStorageView();
                }
            });
        }

        public void EstimateCleanup()
        {
            var rule = BuildRule();
            if (rule.Statuses.Count == 0)
            {
                MessageBox.Show(this, Strings.NoCleanupStatus, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SaveSettings();
            var parallelism = _settings.ParallelQueries;
            // Jobs of the rule statuses in the whole table (when counted): plans the slices of the estimate.
            long? expected = null;
            if (_tableCounts != null && !_tableCounts.Approximate) expected = rule.Statuses.Sum(st => _tableCounts[st]);

            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.CountingOnServer,
                Work = (worker, args) =>
                {
                    var cleanup = new CleanupService(Service, new JobCountService(Service, new JobQueryService(Service))
                    {
                        IsCancelled = () => worker.CancellationPending,
                        Parallelism = parallelism
                    });
                    if (!_snapshotCount.HasValue) _snapshotCount = cleanup.GetSnapshotRowCount();
                    args.Result = cleanup.Estimate(rule, (m, p) => worker.ReportProgress(p, m), expected);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.CountFailed, e.Error);
                        return;
                    }
                    _cleanupCounts = (StatusCounts)e.Result;
                    RefreshStorageView();
                }
            });
        }

        public void PreviewCleanupQuery()
        {
            var rule = BuildRule();
            if (rule.Statuses.Count == 0)
            {
                MessageBox.Show(this, Strings.NoCleanupStatus, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.Working,
                Work = (worker, args) => args.Result = new CleanupService(Service, null).PreviewFetchXml(rule),
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.ActionFailed, e.Error);
                        return;
                    }
                    using (var form = new TextViewerForm(Strings.PreviewQueryTitle, Strings.PreviewQuerySubtitle, (string)e.Result))
                    {
                        form.ShowDialog(this);
                    }
                }
            });
        }

        public void CreateBulkDelete()
        {
            var rule = BuildRule();
            if (rule.Statuses.Count == 0)
            {
                MessageBox.Show(this, Strings.NoCleanupStatus, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var name = string.IsNullOrWhiteSpace(txtJobName.Text) ? Strings.DefaultBulkJobName(rule.OlderThanDays) : txtJobName.Text.Trim();
            var start = dtpStart.Value;
            var recurrenceIndex = Math.Max(0, cboRecurrence.SelectedIndex);
            var recurrence = Recurrences[recurrenceIndex];

            var question = Strings.ConfirmBulkDelete(name, rule, start, cboRecurrence.Text, _cleanupCounts?.Total);
            if (MessageBox.Show(this, question, Strings.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            SaveSettings();

            RunWithOverlay(new WorkAsyncInfo
            {
                Message = Strings.CreatingBulk,
                Work = (worker, args) =>
                {
                    var cleanup = new CleanupService(Service, null);
                    var id = cleanup.CreateBulkDelete(rule, name, start.ToUniversalTime(), recurrence);
                    List<BulkDeleteJobInfo> jobs;
                    try
                    {
                        jobs = cleanup.GetSystemJobBulkDeletes();
                    }
                    catch
                    {
                        jobs = null;
                    }
                    args.Result = Tuple.Create(id, jobs);
                },
                PostWorkCallBack = e =>
                {
                    if (e.Error != null)
                    {
                        ShowError(Strings.BulkFailed, e.Error);
                        return;
                    }
                    var data = (Tuple<Guid, List<BulkDeleteJobInfo>>)e.Result;
                    _createdBulkJobId = data.Item1;
                    lblBulkResult.Text = Strings.BulkCreated(name, start);
                    lnkBulkJob.Visible = true;
                    if (data.Item2 != null) FillBulkJobs(data.Item2);
                    LogInfo("Bulk deletion job created: {0} ({1})", name, data.Item1);
                }
            });
        }

        private void FillBulkJobs(List<BulkDeleteJobInfo> jobs)
        {
            dgvBulkJobs.Rows.Clear();
            foreach (var job in jobs)
            {
                var index = dgvBulkJobs.Rows.Add(job.Name, job.Status, job.IsRecurring ? Strings.Yes : Strings.No, FormatDate(job.NextRun),
                    job.SuccessCount.ToString("N0"), job.FailureCount.ToString("N0"), FormatDate(job.CreatedOn));
                dgvBulkJobs.Rows[index].Tag = job;
            }
            lblBulkList.Text = jobs.Count == 0 ? Strings.NoBulkJobs : Strings.ExistingBulkJobs;
        }

        private void RefreshStorageView()
        {
            if (tileSnapshot == null) return;

            var rows = _snapshotCount ?? _tableCounts?.Total;
            tileSnapshot.Value = rows.HasValue ? rows.Value.ToString("N0") : "–";
            tileTableSize.Value = rows.HasValue ? FormatSize(rows.Value * AverageKb) : "–";
            tileSnapshot.Caption = _snapshotCount.HasValue ? Strings.TileSnapshot : Strings.TileRows;

            if (_tableCounts != null)
            {
                barTableStatuses.Items = JobStatus.All
                    .Select(s => new BreakdownItem { Key = s.ToString(), Label = JobStatus.Label(s), Count = (int)Math.Min(int.MaxValue, _tableCounts[s]), Color = JobStatus.ColorOf(s) })
                    .Where(i => i.Count > 0)
                    .ToList();
                cardTableStatuses.Subtitle = Strings.TableStatusesDone(_tableCounts.Total, _tableCounts.QueryCount, _tableCounts.Approximate);
            }
            else
            {
                barTableStatuses.Items = null;
            }

            if (_cleanupCounts != null)
            {
                var total = _cleanupCounts.Total;
                tileCleanCount.Value = (_cleanupCounts.Approximate ? "≥ " : string.Empty) + total.ToString("N0");
                tileCleanSpace.Value = "≈ " + FormatSize(total * AverageKb);
                lblRuleNote.Text = Strings.EstimateNote(AverageKb, rows.HasValue && rows.Value > 0 ? (double)total / rows.Value : (double?)null);
            }
            else
            {
                tileCleanCount.Value = "–";
                tileCleanSpace.Value = "–";
                lblRuleNote.Text = Strings.RuleNote;
            }
        }

        private static string FormatSize(double kb)
        {
            if (kb >= 1024 * 1024) return $"{kb / 1024 / 1024:N1} GB";
            if (kb >= 1024) return $"{kb / 1024:N0} MB";
            return $"{kb:N0} KB";
        }

        #endregion

        #region Export

        private void Export()
        {
            if (_result == null) return;

            using (var dialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"SystemJobsDoctor_{ConnectionDetail?.ConnectionName ?? "export"}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx".Replace(" ", "_"),
                Title = Strings.ExportExcel
            })
            {
                foreach (var invalid in System.IO.Path.GetInvalidFileNameChars()) dialog.FileName = dialog.FileName.Replace(invalid, '_');
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var path = dialog.FileName;
                var result = _result;
                var organization = ConnectionDetail?.ConnectionName;
                RunWithOverlay(new WorkAsyncInfo
                {
                    Message = Strings.Exporting,
                    Work = (worker, args) => ExportService.Export(path, result, organization),
                    PostWorkCallBack = e =>
                    {
                        if (e.Error != null)
                        {
                            ShowError(Strings.ExportFailed, e.Error);
                            return;
                        }
                        if (MessageBox.Show(this, Strings.ExportDone(path), Strings.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            OpenUrl(path);
                        }
                    }
                });
            }
        }

        #endregion

        #region Background tasks

        /// <summary>
        /// Runs a task in the background behind the loading overlay (replaces the XrmToolBox WorkAsync panel).
        /// The task can be stopped: CancellationPending is raised, and a task stuck in a server call is abandoned after a few seconds.
        /// A task that ends with e.Cancel / OperationCanceledException only shows « stopped »; its callback is not called.
        /// </summary>
        private void RunWithOverlay(WorkAsyncInfo info)
        {
            if (_busy != null) return;

            var task = new BusyTask
            {
                Worker = new System.ComponentModel.BackgroundWorker { WorkerReportsProgress = true, WorkerSupportsCancellation = true }
            };
            _busy = task;

            task.Worker.DoWork += (s, e) =>
            {
                try
                {
                    info.Work(task.Worker, e);
                }
                catch (OperationCanceledException)
                {
                    e.Cancel = true;
                }
            };
            // The worker raises its events on the thread that started it only when that thread has a WinForms
            // synchronization context: they are marshalled explicitly so the UI is always updated on its own thread.
            task.Worker.ProgressChanged += (s, e) => OnUiThread(() =>
            {
                if (_busy == task) overlay.SetProgress(e.UserState as string, e.ProgressPercentage > 0 ? e.ProgressPercentage : (int?)null);
            });
            task.Worker.RunWorkerCompleted += (s, e) => OnUiThread(() =>
            {
                task.Worker.Dispose();
                if (task.Abandoned)
                {
                    LogInfo("Abandoned task finished: {0}", info.Message);
                    return;
                }

                EndBusyTask(task);
                if (IsDisposed) return;
                if (e.Cancelled || e.Error is OperationCanceledException)
                {
                    ShowStopped();
                    return;
                }
                info.PostWorkCallBack?.Invoke(e);
            });

            overlay.Start(this, info.Message, true);
            task.Worker.RunWorkerAsync();
        }

        private void OnUiThread(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

        private void StopBusyTask()
        {
            var task = _busy;
            if (task == null) return;

            task.Worker.CancelAsync();
            overlay.SetStopping();

            // A server call cannot be interrupted: if the task does not stop by itself, the UI is given back and the result ignored.
            var timer = new Timer { Interval = AbandonDelayMs };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (_busy != task || IsDisposed) return;
                task.Abandoned = true;
                EndBusyTask(task);
                ShowStopped();
            };
            timer.Start();
        }

        private void EndBusyTask(BusyTask task)
        {
            if (_busy != task) return;
            _busy = null;
            overlay.Finish();
        }

        private void ShowStopped()
        {
            lblResultInfo.Text = Strings.TaskStopped;
            lblResultInfo.ForeColor = Theme.WarningText;
            LogInfo("Task stopped by the user.");
        }

        #endregion

        #region Helpers

        internal static string FormatDate(DateTime? utc, bool seconds = false)
        {
            if (!utc.HasValue) return string.Empty;
            return utc.Value.ToLocalTime().ToString(seconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 60) return $"{span.TotalSeconds:0.#} s";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min {span.Seconds} s";
            return $"{(int)span.TotalHours} h {span.Minutes} min";
        }

        private void ShowError(string context, Exception error)
        {
            LogError("{0}: {1}", context, error.ToString());
            MessageBox.Show(this, $"{context}.\r\n\r\n{GetErrorMessage(error)}", Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string GetErrorMessage(Exception error)
        {
            switch (error)
            {
                case FaultException<OrganizationServiceFault> fault:
                    return fault.Detail?.Message ?? fault.Message;
                case InvalidOperationException invalid:
                    return invalid.Message;
                default:
                    return error.InnerException != null ? $"{error.Message}\r\n{error.InnerException.Message}" : error.Message;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Strings.LanguageChanged -= Strings_LanguageChanged;
                _searchTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
