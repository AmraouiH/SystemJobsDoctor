using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;
using SystemJobsDoctor.Services;
using SystemJobsDoctor.UI;
using SystemJobsDoctor.UI.Controls;

namespace SystemJobsDoctor.Forms
{
    /// <summary>
    /// Summary of the failed jobs, regenerated when the options change, with copy and save.
    /// </summary>
    [DesignerCategory("Code")]
    public class FailedSummaryForm : ThemedDialog
    {
        private readonly AnalysisResult _result;
        private readonly List<JobRecord> _listJobs;
        private readonly string _listLabel;
        private readonly string _organization;
        private readonly CheckBox _includeSuspended;
        private readonly RadioButton _allJobs;
        private readonly RadioButton _listOnly;
        private readonly TextBox _text;

        /// <param name="listJobs">Jobs of the current job list (filters applied), offered as an alternative scope.</param>
        public FailedSummaryForm(AnalysisResult result, List<JobRecord> listJobs, string listCaption, string listLabel, bool preferList, string organization)
            : base(Strings.SummaryDialogTitle, Strings.SummaryDialogSubtitle)
        {
            _result = result;
            _listJobs = listJobs;
            _listLabel = listLabel;
            _organization = organization;

            ClientSize = new Size(900, 680);
            MinimumSize = new Size(640, 460);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, BackColor = Theme.Canvas };
            _allJobs = new RadioButton { Text = Strings.SummaryScopeAll(result.Jobs.Count), AutoSize = true, Font = Theme.Semibold, Margin = new Padding(0, 8, 16, 0) };
            _listOnly = new RadioButton { Text = listCaption, AutoSize = true, Font = Theme.Semibold, Margin = new Padding(0, 8, 24, 0) };
            _includeSuspended = new CheckBox { Text = Strings.SummaryIncludeSuspended, AutoSize = true, Font = Theme.Semibold, ForeColor = Theme.Suspended, Margin = new Padding(0, 8, 0, 0) };
            var sameScope = listJobs.Count == result.Jobs.Count;
            _listOnly.Enabled = !sameScope;
            _allJobs.Checked = sameScope || !preferList;
            _listOnly.Checked = !_allJobs.Checked;
            options.Controls.AddRange(new Control[] { _allJobs, _listOnly, _includeSuspended });

            _text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = Theme.Mono
            };
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12) };
            host.Controls.Add(_text);

            Body.Controls.Add(host);
            Body.Controls.Add(options);

            AddFooterButton(Strings.Close, ButtonVariant.Secondary, DialogResult.OK);
            var save = AddFooterButton(Strings.SaveAs, ButtonVariant.Secondary, DialogResult.None, IconKind.Export);
            save.Click += (s, e) => Save();
            var copy = AddFooterButton(Strings.Copy, ButtonVariant.Primary, DialogResult.None, IconKind.Copy);
            copy.Click += (s, e) =>
            {
                if (_text.TextLength == 0) return;
                Clipboard.SetText(_text.Text);
                copy.Text = Strings.Copied;
            };

            _allJobs.CheckedChanged += (s, e) => Regenerate();
            _includeSuspended.CheckedChanged += (s, e) => Regenerate();
            Regenerate();
        }

        private void Regenerate()
        {
            var whole = _allJobs.Checked;
            var options = new FailedSummaryOptions
            {
                IncludeSuspended = _includeSuspended.Checked,
                Organization = _organization,
                IsWholeAnalysis = whole,
                ScopeLabel = whole ? Strings.SummaryScopeAllLabel : _listLabel
            };
            var text = FailedSummaryService.Build(whole ? _result.Jobs : _listJobs, _result, options);
            _text.Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            _text.Select(0, 0);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _text.Select(0, 0);
        }

        private void Save()
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = Strings.SummaryFileFilter,
                FileName = $"SystemJobsDoctor_failed_{DateTime.Now:yyyyMMdd_HHmm}.txt"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dialog.FileName, _text.Text, new UTF8Encoding(true));
            }
        }
    }
}
