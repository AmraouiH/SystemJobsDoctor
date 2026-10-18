using System;
using System.Drawing;
using System.Windows.Forms;
using SystemJobsDoctor.UI;
using SystemJobsDoctor.UI.Controls;

namespace SystemJobsDoctor
{
    /// <summary>
    /// Construction of the tool UI (built in code: header, scope card, tabs and the four pages).
    /// Docking rule used everywhere: the Fill control is added first, then the edge controls from the innermost to the outermost.
    /// </summary>
    public partial class SystemJobsDoctorControl
    {
        #region Controls

        private ToolStrip tsHeader;
        private ToolStripButton tsbClose;
        private ToolStripButton tsbAnalyze;
        private ToolStripButton tsbExport;
        private ToolStripButton tsbSummary;
        private ToolStripButton tsbOptions;
        private ToolStripDropDownButton tsddLanguage;
        private ToolStripMenuItem tsmiEnglish;
        private ToolStripMenuItem tsmiFrench;
        private ToolStripButton tsbAuthor;
        private ToolStripButton tsbDonate;
        private ToolStripButton tsbHelp;

        private CardPanel cardScope;
        private Label lblPeriod, lblDateField, lblFrom, lblTo, lblPeriodHint, lblType, lblTable, lblName, lblMax, lblStatuses;
        private ComboBox cboPeriod, cboDateField, cboType, cboTable;
        private DateTimePicker dtpFrom, dtpTo;
        private TextBox txtName;
        private NumericUpDown numMax;
        private ModernButton btnAnalyze;
        private FlowLayoutPanel flpStatuses;
        private LinkLabel lnkProblems, lnkAllStatuses;

        private SegmentedTabs tabs;
        private Label lblResultInfo;
        private Panel pnlPages;
        private Panel pageDashboard, pageErrors, pageJobs, pageStorage;

        // Dashboard
        private StatTile tileScope, tileWaiting, tileSuspended, tileRunning, tileFailed, tileCanceled, tileSucceeded;
        private Label lblCountsNote;
        private CardPanel cardTimeline, cardByStatus, cardByType, cardByTable, cardByProcess;
        private TimelineChart chartTimeline;
        private BarList barStatus, barType, barTable, barProcess;

        // Errors
        private SplitContainer splitErrors, splitErrorsRight;
        private CardPanel cardGroups, cardTopFailing, cardGroupDetail;
        private DataGridView dgvGroups;
        private BarList barTopFailing;
        private TextBox txtGroupSample;
        private Label lblGroupInfo;
        private ModernButton btnGroupJobs, btnGroupResume, btnGroupCancel, btnGroupDelete, btnGroupCopy;

        // Jobs
        private TextBox txtJobSearch;
        private ComboBox cboJobTable;
        private Label lblQuickFilter, lblJobCount;
        private ModernButton btnClearQuickFilter, btnResume, btnCancel, btnPostpone, btnDelete, btnJobSummary;
        private SplitContainer splitJobs;
        private CardPanel cardJobs, cardJobDetail;
        private DataGridView dgvJobs, dgvDetail;
        private TextBox txtJobMessage;
        private ModernButton btnSummarizeJob, btnOpenRecord, btnOpenJob, btnOpenProcess, btnTraceLogs, btnCopyDetails;
        private ContextMenuStrip cmsJobs;
        private ToolStripMenuItem tsmiResume, tsmiCancel, tsmiPostpone, tsmiDelete, tsmiOpenRecord, tsmiOpenJob, tsmiOpenProcess,
            tsmiTraceLogs, tsmiSameError, tsmiSameProcess, tsmiCopyError, tsmiCopyIds, tsmiSummarize;

        // Storage
        private CardPanel cardTableSize, cardRule, cardBulk;
        private StatTile tileSnapshot, tileTableSize, tileCleanCount, tileCleanSpace;
        private Label lblAvgKb, lblAdminMb, lblStorageNote, lblOlderThan, lblCleanType, lblCleanStatuses, lblRuleNote;
        private NumericUpDown numAvgKb, numAdminMb, numOlderThan;
        private ModernButton btnRefreshStorage, btnCountAll, btnEstimate, btnPreviewQuery, btnCreateBulk;
        private BarList barTableStatuses;
        private CheckBox chkCleanSucceeded, chkCleanCanceled, chkCleanFailed;
        private ComboBox cboCleanType, cboRecurrence;
        private Label lblJobName, lblStart, lblRecurrence, lblBulkResult, lblBulkList;
        private TextBox txtJobName;
        private DateTimePicker dtpStart;
        private LinkLabel lnkBulkJob;
        private DataGridView dgvBulkJobs;

        private ToolTip toolTip;
        private LoadingOverlay overlay;

        #endregion

        private void BuildLayout()
        {
            SuspendLayout();
            Font = Theme.Base;
            BackColor = Theme.Canvas;
            Size = new Size(1400, 860);
            toolTip = new ToolTip { AutoPopDelay = 15000 };

            BuildHeader();

            // Below this size the body scrolls instead of crushing the cards.
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 10, 12, 12), BackColor = Theme.Canvas, AutoScroll = true, AutoScrollMinSize = new Size(1000, 580) };

            pnlPages = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
            pageDashboard = NewPage();
            pageErrors = NewPage();
            pageJobs = NewPage();
            pageStorage = NewPage();
            pnlPages.Controls.AddRange(new Control[] { pageDashboard, pageErrors, pageJobs, pageStorage });

            BuildDashboard();
            BuildErrors();
            BuildJobs();
            BuildStorage();

            var tabsHost = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.Canvas, Padding = new Padding(0, 4, 0, 0) };
            tabs = new SegmentedTabs { Dock = DockStyle.Fill };
            tabs.Tabs.Add(new SegmentedTabs.Tab { Glyph = IconKind.Chart });
            tabs.Tabs.Add(new SegmentedTabs.Tab { Glyph = IconKind.Alert });
            tabs.Tabs.Add(new SegmentedTabs.Tab { Glyph = IconKind.List, BadgeColor = Theme.Accent });
            tabs.Tabs.Add(new SegmentedTabs.Tab { Glyph = IconKind.Database });
            lblResultInfo = new Label { Dock = DockStyle.Right, Width = 520, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.Muted, Font = Theme.Small };
            tabsHost.Controls.Add(tabs);
            tabsHost.Controls.Add(lblResultInfo);

            BuildScope();

            body.Controls.Add(pnlPages);
            body.Controls.Add(tabsHost);
            body.Controls.Add(cardScope);

            Controls.Add(body);
            Controls.Add(tsHeader);

            // Over everything (header included) while a task runs.
            overlay = new LoadingOverlay();
            Controls.Add(overlay);
            overlay.BringToFront();
            ResumeLayout(false);
        }

        private static Panel NewPage() => new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Visible = false };

        #region Header

        /// <summary>
        /// Command bar of Entity Fields Analyser: white Fluent surface, close glyph, accent « Analyze » button,
        /// actions with icons, and the Donate / Help / author links on the right.
        /// </summary>
        private void BuildHeader()
        {
            tsHeader = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                AutoSize = false,
                Height = 48,
                Padding = new Padding(10, 6, 10, 6),
                ImageScalingSize = new Size(16, 16),
                BackColor = Fluent.Surface
            };

            tsbClose = new ToolStripButton
            {
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                ImageScaling = ToolStripItemImageScaling.None,
                Margin = new Padding(0, 0, 2, 0),
                Padding = new Padding(6, 0, 6, 0),
                Overflow = ToolStripItemOverflow.Never
            };
            tsbAnalyze = HeaderButton(FluentToolStripRenderer.Primary);
            tsbAnalyze.Padding = new Padding(10, 0, 12, 0);
            tsbAnalyze.Margin = new Padding(2, 0, 4, 0);
            tsbSummary = HeaderButton(null);
            tsbExport = HeaderButton(null);
            tsbOptions = HeaderButton(null);
            tsddLanguage = new ToolStripDropDownButton
            {
                ImageScaling = ToolStripItemImageScaling.None,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Margin = new Padding(2, 0, 2, 0),
                Padding = new Padding(8, 0, 8, 0)
            };
            tsmiEnglish = new ToolStripMenuItem(Localization.Strings.EnglishName);
            tsmiFrench = new ToolStripMenuItem(Localization.Strings.FrenchName);
            tsddLanguage.DropDownItems.AddRange(new ToolStripItem[] { tsmiEnglish, tsmiFrench });

            // Right-aligned items are laid out from the right edge: author, then Help, then Donate.
            tsbAuthor = HeaderButton(FluentToolStripRenderer.Link);
            tsbAuthor.Alignment = ToolStripItemAlignment.Right;
            tsbAuthor.Margin = new Padding(2, 0, 0, 0);
            tsbHelp = HeaderButton(FluentToolStripRenderer.Link);
            tsbHelp.Alignment = ToolStripItemAlignment.Right;
            tsbDonate = HeaderButton(FluentToolStripRenderer.Link);
            tsbDonate.Alignment = ToolStripItemAlignment.Right;
            foreach (var link in new[] { tsbAuthor, tsbHelp, tsbDonate }) link.Padding = new Padding(6, 0, 6, 0);

            tsHeader.Items.AddRange(new ToolStripItem[]
            {
                tsbClose, new ToolStripSeparator { Margin = new Padding(8, 0, 8, 0) }, tsbAnalyze, tsbSummary, tsbExport,
                new ToolStripSeparator { Margin = new Padding(8, 0, 8, 0) }, tsbOptions, tsddLanguage,
                tsbAuthor, tsbHelp, tsbDonate
            });
        }

        private static ToolStripButton HeaderButton(string tag) => new ToolStripButton
        {
            ImageScaling = ToolStripItemImageScaling.None,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Margin = new Padding(2, 0, 2, 0),
            Padding = new Padding(8, 0, 8, 0),
            Tag = tag
        };

        #endregion

        #region Scope

        private void BuildScope()
        {
            cardScope = new CardPanel { Dock = DockStyle.Top, Height = 170, Step = 0 };

            // Row 1: period (preset, date column, start and end dates)
            var row1 = WrapRow();
            lblPeriod = FieldLabel();
            cboPeriod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(0, 6, 10, 0) };
            lblDateField = FieldLabel();
            cboDateField = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Margin = new Padding(0, 6, 18, 0) };
            lblFrom = FieldLabel();
            dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", ShowCheckBox = true, Width = 150, Margin = new Padding(0, 6, 14, 0) };
            lblTo = FieldLabel();
            dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", ShowCheckBox = true, Width = 150, Margin = new Padding(0, 6, 14, 0) };
            lblPeriodHint = new Label { AutoSize = true, ForeColor = Theme.Subtle, Font = Theme.Small, Margin = new Padding(0, 11, 0, 0) };
            row1.Controls.AddRange(new Control[] { lblPeriod, cboPeriod, lblDateField, cboDateField, lblFrom, dtpFrom, lblTo, dtpTo, lblPeriodHint });

            // Row 2: type, table, name, limit
            var row2 = WrapRow();
            lblType = FieldLabel();
            cboType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, Margin = new Padding(0, 6, 14, 0) };
            lblTable = FieldLabel();
            cboTable = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Width = 250,
                MaxDropDownItems = 20,
                Margin = new Padding(0, 6, 14, 0)
            };
            lblName = FieldLabel();
            txtName = new TextBox { Width = 170, Margin = new Padding(0, 7, 14, 0) };
            lblMax = FieldLabel();
            numMax = new NumericUpDown { Minimum = 100, Maximum = 500000, Increment = 5000, Width = 80, ThousandsSeparator = true, Margin = new Padding(0, 7, 14, 0) };
            btnAnalyze = new ModernButton { Variant = ButtonVariant.Primary, Glyph = IconKind.Search, AutoSize = true, MinimumSize = new Size(110, 32), Height = 32, Margin = new Padding(0, 3, 0, 0) };
            row2.Controls.AddRange(new Control[] { lblType, cboType, lblTable, cboTable, lblName, txtName, lblMax, numMax, btnAnalyze });

            // Row 3: statuses
            var row3 = WrapRow();
            lblStatuses = FieldLabel();
            lblStatuses.Margin = new Padding(0, 8, 8, 0);
            // The status boxes are direct children of the row so they wrap one by one on narrow screens.
            flpStatuses = row3;
            lnkProblems = QuickLink();
            lnkAllStatuses = QuickLink();
            // Quick links first: they stay visible whatever the number of status lines.
            row3.Controls.AddRange(new Control[] { lblStatuses, lnkProblems, lnkAllStatuses });

            // Docked Top: added from the bottom row to the top row.
            scopeRows = new[] { row1, row2, row3 };
            cardScope.Controls.Add(row3);
            cardScope.Controls.Add(row2);
            cardScope.Controls.Add(row1);
        }

        private FlowLayoutPanel[] scopeRows;

        /// <summary>Row of fields or buttons that wraps on narrow screens; its height follows the content.</summary>
        private static FlowLayoutPanel WrapRow() => new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Card,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, 2)
        };

        private static Label FieldLabel() => new Label
        {
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Semibold,
            Margin = new Padding(0, 10, 6, 0)
        };

        private static LinkLabel QuickLink() => new LinkLabel
        {
            AutoSize = true,
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.AccentPressed,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = new Padding(6, 8, 6, 0)
        };

        #endregion

        #region Dashboard

        private void BuildDashboard()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, BackColor = Theme.Canvas, Padding = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            for (var i = 0; i < 4; i++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            var tilesHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Margin = new Padding(0, 0, 0, 4) };
            tilesRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Canvas, Margin = Padding.Empty };
            var tiles = tilesRow;
            tileScope = Tile(Theme.Accent);
            tileWaiting = Tile(Theme.Waiting);
            tileSuspended = Tile(Theme.Suspended);
            tileRunning = Tile(Theme.Running);
            tileFailed = Tile(Theme.Danger);
            tileCanceled = Tile(Theme.Subtle);
            tileSucceeded = Tile(Theme.Success);
            tiles.Controls.AddRange(new Control[] { tileScope, tileWaiting, tileSuspended, tileRunning, tileFailed, tileCanceled, tileSucceeded });
            lblCountsNote = new Label { Dock = DockStyle.Bottom, Height = 18, ForeColor = Theme.Muted, Font = Theme.Small, AutoEllipsis = true };
            tilesHost.Controls.Add(tiles);
            tilesHost.Controls.Add(lblCountsNote);
            layout.Controls.Add(tilesHost, 0, 0);
            layout.SetColumnSpan(tilesHost, 4);

            cardTimeline = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 6) };
            chartTimeline = new TimelineChart { Dock = DockStyle.Fill };
            chartTimeline.SeriesList.Add(new TimelineChart.Series { Color = Theme.Danger, Value = b => b.Failed });
            chartTimeline.SeriesList.Add(new TimelineChart.Series { Color = Theme.Suspended, Value = b => b.Suspended });
            chartTimeline.SeriesList.Add(new TimelineChart.Series { Color = Theme.Waiting, Value = b => b.Waiting });
            chartTimeline.SeriesList.Add(new TimelineChart.Series { Color = Theme.Subtle, Value = b => b.Other });
            chartTimeline.SeriesList.Add(new TimelineChart.Series { Color = Theme.Success, Value = b => b.Succeeded });
            cardTimeline.Controls.Add(chartTimeline);
            layout.Controls.Add(cardTimeline, 0, 1);
            layout.SetColumnSpan(cardTimeline, 4);

            cardByStatus = BreakdownCard(out barStatus, new Padding(0, 0, 5, 0));
            cardByType = BreakdownCard(out barType, new Padding(5, 0, 5, 0));
            cardByTable = BreakdownCard(out barTable, new Padding(5, 0, 5, 0));
            cardByProcess = BreakdownCard(out barProcess, new Padding(5, 0, 0, 0));
            layout.Controls.Add(cardByStatus, 0, 2);
            layout.Controls.Add(cardByType, 1, 2);
            layout.Controls.Add(cardByTable, 2, 2);
            layout.Controls.Add(cardByProcess, 3, 2);

            pageDashboard.Controls.Add(layout);
        }

        private FlowLayoutPanel tilesRow;

        private static StatTile Tile(Color tint) => new StatTile
        {
            Tint = tint,
            Size = new Size(168, 64),
            Margin = new Padding(0, 2, 10, 2),
            Cursor = Cursors.Hand
        };

        private static CardPanel BreakdownCard(out BarList list, Padding margin)
        {
            var card = new CardPanel { Dock = DockStyle.Fill, Margin = margin };
            list = new BarList { Dock = DockStyle.Fill };
            card.Controls.Add(list);
            return card;
        }

        #endregion

        #region Errors

        private void BuildErrors()
        {
            splitErrors = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Theme.Canvas, SplitterWidth = 8, FixedPanel = FixedPanel.Panel2 };
            splitErrors.Panel1.BackColor = Theme.Canvas;
            splitErrors.Panel2.BackColor = Theme.Canvas;

            cardGroups = new CardPanel { Dock = DockStyle.Fill };
            dgvGroups = NewGrid();
            dgvGroups.Columns.AddRange(
                TextColumn("colGroupCount", 70, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colGroupFailed", 62, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colGroupSuspended", 76, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colGroupSignature", 0),
                TextColumn("colGroupProcess", 200),
                TextColumn("colGroupProcesses", 74, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colGroupTables", 140),
                TextColumn("colGroupLast", 120));
            dgvGroups.Columns["colGroupSignature"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvGroups.Columns["colGroupSignature"].MinimumWidth = 200;
            dgvGroups.Columns["colGroupCount"].DefaultCellStyle.Font = Theme.Semibold;
            dgvGroups.Columns["colGroupFailed"].DefaultCellStyle.ForeColor = Theme.Danger;
            dgvGroups.Columns["colGroupSuspended"].DefaultCellStyle.ForeColor = Theme.Suspended;
            cardGroups.Controls.Add(dgvGroups);
            splitErrors.Panel1.Controls.Add(cardGroups);

            splitErrorsRight = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Theme.Canvas, SplitterWidth = 8 };
            splitErrorsRight.Panel1.BackColor = Theme.Canvas;
            splitErrorsRight.Panel2.BackColor = Theme.Canvas;

            cardTopFailing = new CardPanel { Dock = DockStyle.Fill };
            barTopFailing = new BarList { Dock = DockStyle.Fill };
            cardTopFailing.Controls.Add(barTopFailing);
            splitErrorsRight.Panel1.Controls.Add(cardTopFailing);

            cardGroupDetail = new CardPanel { Dock = DockStyle.Fill };
            txtGroupSample = NewMessageBox();
            lblGroupInfo = new Label { Dock = DockStyle.Top, Height = 40, ForeColor = Theme.Muted, Font = Theme.Small, AutoEllipsis = true };
            var groupButtons = ButtonRow();
            btnGroupJobs = SmallButton(IconKind.List, ButtonVariant.Primary);
            btnGroupResume = SmallButton(IconKind.Play, ButtonVariant.Success);
            btnGroupCancel = SmallButton(IconKind.Stop, ButtonVariant.Secondary);
            btnGroupDelete = SmallButton(IconKind.Trash, ButtonVariant.Danger);
            btnGroupCopy = IconButton(IconKind.Copy);
            groupButtons.Controls.AddRange(new Control[] { btnGroupJobs, btnGroupResume, btnGroupCancel, btnGroupDelete, btnGroupCopy });
            cardGroupDetail.Controls.Add(txtGroupSample);
            cardGroupDetail.Controls.Add(lblGroupInfo);
            cardGroupDetail.Controls.Add(groupButtons);
            splitErrorsRight.Panel2.Controls.Add(cardGroupDetail);

            splitErrors.Panel2.Controls.Add(splitErrorsRight);
            pageErrors.Controls.Add(splitErrors);
        }

        #endregion

        #region Jobs

        private void BuildJobs()
        {
            splitJobs = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, BackColor = Theme.Canvas, SplitterWidth = 8, FixedPanel = FixedPanel.Panel2 };
            splitJobs.Panel1.BackColor = Theme.Canvas;
            splitJobs.Panel2.BackColor = Theme.Canvas;

            cardJobs = new CardPanel { Dock = DockStyle.Fill };

            // Wraps on narrow screens: AutoSize + Dock Top makes the height follow the content.
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = Theme.Card, Margin = Padding.Empty, Padding = new Padding(0, 0, 0, 4) };
            txtJobSearch = new TextBox { Width = 210, Margin = new Padding(0, 7, 8, 0) };
            cboJobTable = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, DropDownWidth = 320, MaxDropDownItems = 20, Margin = new Padding(0, 6, 8, 0) };
            lblQuickFilter = new Label
            {
                AutoSize = true,
                BackColor = Theme.AccentSoft,
                ForeColor = Theme.Accent,
                Font = Theme.Semibold,
                Padding = new Padding(8, 4, 8, 4),
                Margin = new Padding(0, 6, 0, 0),
                Visible = false
            };
            btnClearQuickFilter = new ModernButton { Variant = ButtonVariant.Ghost, Glyph = IconKind.Close, Size = new Size(28, 28), Margin = new Padding(2, 4, 10, 0), Visible = false };
            lblJobCount = new Label { AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 11, 14, 0) };
            btnResume = SmallButton(IconKind.Play, ButtonVariant.Success);
            btnCancel = SmallButton(IconKind.Stop, ButtonVariant.Secondary);
            btnPostpone = SmallButton(IconKind.Clock, ButtonVariant.Secondary);
            btnDelete = SmallButton(IconKind.Trash, ButtonVariant.Danger);
            btnJobSummary = SmallButton(IconKind.Log, ButtonVariant.Ghost);
            foreach (var button in new[] { btnResume, btnCancel, btnPostpone, btnDelete, btnJobSummary }) button.Margin = new Padding(0, 4, 8, 0);
            toolbar.Controls.AddRange(new Control[] { txtJobSearch, cboJobTable, lblQuickFilter, btnClearQuickFilter, lblJobCount, btnResume, btnCancel, btnPostpone, btnDelete, btnJobSummary });

            dgvJobs = NewGrid();
            dgvJobs.VirtualMode = true;
            dgvJobs.MultiSelect = true;
            dgvJobs.Columns.AddRange(
                TextColumn("colJobStatus", 150),
                TextColumn("colJobName", 260),
                TextColumn("colJobType", 140),
                TextColumn("colJobTable", 120),
                TextColumn("colJobRecord", 180),
                TextColumn("colJobOwner", 130),
                TextColumn("colJobCreated", 120),
                TextColumn("colJobCompleted", 120),
                TextColumn("colJobPostpone", 120),
                TextColumn("colJobRetries", 60, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colJobError", 0));
            dgvJobs.Columns["colJobError"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvJobs.Columns["colJobError"].MinimumWidth = 220;
            dgvJobs.Columns["colJobError"].DefaultCellStyle.ForeColor = Theme.Danger;
            dgvJobs.Columns["colJobName"].DefaultCellStyle.Font = Theme.Semibold;
            foreach (DataGridViewColumn column in dgvJobs.Columns) column.SortMode = DataGridViewColumnSortMode.Programmatic;

            cardJobs.Controls.Add(dgvJobs);
            cardJobs.Controls.Add(toolbar);
            splitJobs.Panel1.Controls.Add(cardJobs);

            cardJobDetail = new CardPanel { Dock = DockStyle.Fill };
            var detailSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, BackColor = Theme.Card, SplitterWidth = 10 };
            detailSplit.Panel1.BackColor = Theme.Card;
            detailSplit.Panel2.BackColor = Theme.Card;
            dgvDetail = NewGrid();
            dgvDetail.ColumnHeadersVisible = false;
            dgvDetail.RowTemplate.Height = 24;
            dgvDetail.Columns.AddRange(TextColumn("colKey", 130), TextColumn("colValue", 0));
            dgvDetail.Columns["colKey"].DefaultCellStyle.ForeColor = Theme.Muted;
            dgvDetail.Columns["colKey"].DefaultCellStyle.Font = Theme.Small;
            dgvDetail.Columns["colValue"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvDetail.SelectionMode = DataGridViewSelectionMode.CellSelect;
            detailSplit.Panel1.Controls.Add(dgvDetail);
            txtJobMessage = NewMessageBox();
            detailSplit.Panel2.Controls.Add(txtJobMessage);
            detailSplit.SizeChanged += (s, e) =>
            {
                var distance = Math.Max(200, Math.Min(480, detailSplit.Width * 2 / 5));
                if (detailSplit.Width > distance + 100) detailSplit.SplitterDistance = distance;
            };

            var detailButtons = ButtonRow();
            btnOpenRecord = SmallButton(IconKind.Link, ButtonVariant.Secondary);
            btnOpenJob = SmallButton(IconKind.Link, ButtonVariant.Secondary);
            btnOpenProcess = SmallButton(IconKind.Link, ButtonVariant.Secondary);
            btnSummarizeJob = SmallButton(IconKind.Alert, ButtonVariant.Primary);
            btnTraceLogs = SmallButton(IconKind.Log, ButtonVariant.Secondary);
            btnCopyDetails = IconButton(IconKind.Copy);
            detailButtons.Controls.AddRange(new Control[] { btnSummarizeJob, btnTraceLogs, btnOpenRecord, btnOpenJob, btnOpenProcess, btnCopyDetails });

            cardJobDetail.Controls.Add(detailSplit);
            cardJobDetail.Controls.Add(detailButtons);
            splitJobs.Panel2.Controls.Add(cardJobDetail);

            cmsJobs = new ContextMenuStrip();
            tsmiResume = new ToolStripMenuItem();
            tsmiCancel = new ToolStripMenuItem();
            tsmiPostpone = new ToolStripMenuItem();
            tsmiDelete = new ToolStripMenuItem();
            tsmiOpenRecord = new ToolStripMenuItem();
            tsmiOpenJob = new ToolStripMenuItem();
            tsmiOpenProcess = new ToolStripMenuItem();
            tsmiTraceLogs = new ToolStripMenuItem();
            tsmiSummarize = new ToolStripMenuItem { Font = Theme.Semibold };
            tsmiSameError = new ToolStripMenuItem();
            tsmiSameProcess = new ToolStripMenuItem();
            tsmiCopyError = new ToolStripMenuItem();
            tsmiCopyIds = new ToolStripMenuItem();
            cmsJobs.Items.AddRange(new ToolStripItem[]
            {
                tsmiSummarize, new ToolStripSeparator(),
                tsmiResume, tsmiCancel, tsmiPostpone, tsmiDelete, new ToolStripSeparator(),
                tsmiOpenRecord, tsmiOpenJob, tsmiOpenProcess, tsmiTraceLogs, new ToolStripSeparator(),
                tsmiSameError, tsmiSameProcess, new ToolStripSeparator(), tsmiCopyError, tsmiCopyIds
            });
            dgvJobs.ContextMenuStrip = cmsJobs;

            pageJobs.Controls.Add(splitJobs);
        }

        #endregion

        #region Storage

        private void BuildStorage()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Theme.Canvas };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Table size
            cardTableSize = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            var sizeTop = WrapRow();
            tileSnapshot = Tile(Theme.Accent);
            tileSnapshot.Cursor = Cursors.Default;
            tileTableSize = Tile(Theme.Suspended);
            tileTableSize.Cursor = Cursors.Default;
            sizeTop.Controls.AddRange(new Control[] { tileSnapshot, tileTableSize });

            var sizeInputs = WrapRow();
            lblAvgKb = FieldLabel();
            numAvgKb = new NumericUpDown { Minimum = 1, Maximum = 2000, Width = 64, Margin = new Padding(0, 7, 14, 0) };
            lblAdminMb = FieldLabel();
            numAdminMb = new NumericUpDown { Minimum = 0, Maximum = 10000000, Width = 90, ThousandsSeparator = true, Margin = new Padding(0, 7, 0, 0) };
            sizeInputs.Controls.AddRange(new Control[] { lblAvgKb, numAvgKb, lblAdminMb, numAdminMb });

            var sizeButtons = ButtonRow();
            sizeButtons.Dock = DockStyle.Top;
            sizeButtons.Padding = new Padding(0, 4, 0, 0);
            btnRefreshStorage = SmallButton(IconKind.Reload, ButtonVariant.Secondary);
            btnCountAll = SmallButton(IconKind.Chart, ButtonVariant.Primary);
            sizeButtons.Controls.AddRange(new Control[] { btnRefreshStorage, btnCountAll });

            lblStorageNote = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = Theme.Small };
            cardTableSize.Controls.Add(lblStorageNote);
            cardTableSize.Controls.Add(sizeButtons);
            cardTableSize.Controls.Add(sizeInputs);
            cardTableSize.Controls.Add(sizeTop);
            layout.Controls.Add(cardTableSize, 0, 0);

            // Statuses of the whole table
            var cardStatuses = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 6, 0), Tag = "statuses" };
            barTableStatuses = new BarList { Dock = DockStyle.Fill, Clickable = false };
            cardStatuses.Controls.Add(barTableStatuses);
            layout.Controls.Add(cardStatuses, 0, 1);
            cardTableStatuses = cardStatuses;

            // Cleanup rule
            cardRule = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
            var ruleRow1 = WrapRow();
            lblCleanStatuses = FieldLabel();
            chkCleanSucceeded = StatusCheck(Theme.SuccessText);
            chkCleanCanceled = StatusCheck(Theme.Muted);
            chkCleanFailed = StatusCheck(Theme.Danger);
            ruleRow1.Controls.AddRange(new Control[] { lblCleanStatuses, chkCleanSucceeded, chkCleanCanceled, chkCleanFailed });

            var ruleRow2 = WrapRow();
            lblOlderThan = FieldLabel();
            numOlderThan = new NumericUpDown { Minimum = 0, Maximum = 3650, Width = 64, Margin = new Padding(0, 7, 14, 0) };
            lblCleanType = FieldLabel();
            cboCleanType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Margin = new Padding(0, 6, 14, 0) };
            btnEstimate = SmallButton(IconKind.Search, ButtonVariant.Primary);
            btnEstimate.Margin = new Padding(0, 3, 8, 0);
            btnPreviewQuery = SmallButton(IconKind.Fields, ButtonVariant.Ghost);
            btnPreviewQuery.Margin = new Padding(0, 3, 0, 0);
            ruleRow2.Controls.AddRange(new Control[] { lblOlderThan, numOlderThan, lblCleanType, cboCleanType, btnEstimate, btnPreviewQuery });

            var ruleTiles = WrapRow();
            tileCleanCount = Tile(Theme.Danger);
            tileCleanCount.Cursor = Cursors.Default;
            tileCleanSpace = Tile(Theme.Success);
            tileCleanSpace.Cursor = Cursors.Default;
            ruleTiles.Controls.AddRange(new Control[] { tileCleanCount, tileCleanSpace });

            lblRuleNote = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = Theme.Small };
            cardRule.Controls.Add(lblRuleNote);
            cardRule.Controls.Add(ruleTiles);
            cardRule.Controls.Add(ruleRow2);
            cardRule.Controls.Add(ruleRow1);
            layout.Controls.Add(cardRule, 1, 0);

            // Bulk deletion job
            cardBulk = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(6, 10, 0, 0) };
            var bulkRow1 = WrapRow();
            lblJobName = FieldLabel();
            txtJobName = new TextBox { Width = 380, Margin = new Padding(0, 7, 14, 0) };
            lblStart = FieldLabel();
            dtpStart = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Width = 135, Margin = new Padding(0, 6, 14, 0) };
            bulkRow1.Controls.AddRange(new Control[] { lblJobName, txtJobName, lblStart, dtpStart });

            var bulkRow2 = WrapRow();
            lblRecurrence = FieldLabel();
            cboRecurrence = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(0, 6, 14, 0) };
            btnCreateBulk = SmallButton(IconKind.Trash, ButtonVariant.Primary);
            btnCreateBulk.Margin = new Padding(0, 3, 12, 0);
            lnkBulkJob = QuickLink();
            lnkBulkJob.Margin = new Padding(0, 10, 0, 0);
            lnkBulkJob.Visible = false;
            bulkRow2.Controls.AddRange(new Control[] { lblRecurrence, cboRecurrence, btnCreateBulk, lnkBulkJob });

            lblBulkResult = new Label { Dock = DockStyle.Top, Height = 22, ForeColor = Theme.SuccessText, Font = Theme.Semibold, AutoEllipsis = true };
            lblBulkList = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = Theme.Muted, Font = Theme.SmallBold, TextAlign = ContentAlignment.BottomLeft };

            dgvBulkJobs = NewGrid();
            dgvBulkJobs.Columns.AddRange(
                TextColumn("colBulkName", 0),
                TextColumn("colBulkStatus", 110),
                TextColumn("colBulkRecurring", 80),
                TextColumn("colBulkNextRun", 125),
                TextColumn("colBulkDeleted", 90, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colBulkFailures", 70, DataGridViewContentAlignment.MiddleRight),
                TextColumn("colBulkCreated", 125));
            dgvBulkJobs.Columns["colBulkName"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgvBulkJobs.Columns["colBulkName"].MinimumWidth = 160;

            cardBulk.Controls.Add(dgvBulkJobs);
            cardBulk.Controls.Add(lblBulkList);
            cardBulk.Controls.Add(lblBulkResult);
            cardBulk.Controls.Add(bulkRow2);
            cardBulk.Controls.Add(bulkRow1);
            layout.Controls.Add(cardBulk, 1, 1);

            // On small screens the page scrolls instead of crushing the bulk deletion card.
            pageStorage.AutoScroll = true;
            pageStorage.AutoScrollMinSize = new Size(0, 690);
            pageStorage.Controls.Add(layout);
        }

        private CardPanel cardTableStatuses;

        private static CheckBox StatusCheck(Color color) => new CheckBox
        {
            AutoSize = true,
            ForeColor = color,
            Font = Theme.Semibold,
            Margin = new Padding(0, 8, 14, 0)
        };

        #endregion

        #region Helpers

        private static DataGridView NewGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            Theme.StyleGrid(grid, true);
            return grid;
        }

        private static DataGridViewTextBoxColumn TextColumn(string name, int width, DataGridViewContentAlignment alignment = DataGridViewContentAlignment.MiddleLeft)
        {
            var column = new DataGridViewTextBoxColumn { Name = name, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic };
            if (width > 0) column.Width = width;
            column.DefaultCellStyle.Alignment = alignment;
            if (alignment != DataGridViewContentAlignment.MiddleLeft) column.HeaderCell.Style.Alignment = alignment;
            return column;
        }

        private static TextBox NewMessageBox() => new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(248, 249, 252),
            ForeColor = Theme.Text,
            Font = Theme.Mono
        };

        private static FlowLayoutPanel ButtonRow() => new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            BackColor = Theme.Card,
            Padding = new Padding(0, 8, 0, 0)
        };

        /// <summary>Square icon-only button (its text is shown as a tooltip by the control).</summary>
        private static ModernButton IconButton(IconKind glyph) => new ModernButton
        {
            Variant = ButtonVariant.Ghost,
            Glyph = glyph,
            Size = new Size(36, 32),
            Margin = new Padding(0, 0, 8, 6)
        };

        private static ModernButton SmallButton(IconKind glyph, ButtonVariant variant) => new ModernButton
        {
            Variant = variant,
            Glyph = glyph,
            AutoSize = true,
            MinimumSize = new Size(80, 32),
            Height = 32,
            Margin = new Padding(0, 0, 8, 6)
        };

        #endregion
    }
}
