using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
    /// Plug-in trace logs of a system job (same correlation id / request id), with the message block and exception of each.
    /// </summary>
    [DesignerCategory("Code")]
    public class TraceLogForm : ThemedDialog
    {
        private readonly DataGridView _grid;
        private readonly TextBox _detail;

        public TraceLogForm(JobRecord job, List<TraceLogEntry> traces, int? traceSetting, string listUrl)
            : base(Strings.TraceLogs, Strings.TraceSubtitle(job.ProcessName ?? job.Name, traces.Count))
        {
            ClientSize = new Size(1000, 660);
            MinimumSize = new Size(760, 480);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            var note = new Label
            {
                Dock = DockStyle.Top,
                Height = traces.Count == 0 || traceSetting == 0 ? 48 : 0,
                ForeColor = Theme.WarningText,
                BackColor = Theme.WarningSoft,
                Padding = new Padding(8, 6, 8, 6),
                Text = traceSetting == 0 ? Strings.TraceDisabled : traces.Count == 0 ? Strings.NoTraces : string.Empty
            };

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 8, BackColor = Theme.Canvas };
            split.Panel1.BackColor = Theme.Card;
            split.Panel2.BackColor = Theme.Card;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            Theme.StyleGrid(_grid, true);
            _grid.Columns.Add("created", Strings.ColCreated);
            _grid.Columns.Add("type", Strings.TraceType);
            _grid.Columns.Add("message", Strings.DetailMessage);
            _grid.Columns.Add("entity", Strings.ColTable);
            _grid.Columns.Add("mode", Strings.TraceMode);
            _grid.Columns.Add("depth", Strings.DetailDepth);
            _grid.Columns.Add("duration", Strings.TraceDuration);
            _grid.Columns.Add("exception", Strings.TraceException);
            // Proportional widths: every header stays readable whatever the window width.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ColumnHeadersHeight = 34;
            var weights = new[] { 130, 220, 80, 90, 70, 75, 75, 80 };
            for (var i = 0; i < weights.Length; i++)
            {
                _grid.Columns[i].FillWeight = weights[i];
                _grid.Columns[i].MinimumWidth = 60;
            }
            foreach (var trace in traces)
            {
                var index = _grid.Rows.Add(SystemJobsDoctorControl.FormatDate(trace.CreatedOn, true), trace.TypeName, trace.MessageName, trace.PrimaryEntity,
                    trace.Mode, trace.Depth, trace.DurationMs.HasValue ? $"{trace.DurationMs} ms" : string.Empty, trace.HasException ? Strings.Yes : string.Empty);
                _grid.Rows[index].Tag = trace;
                if (trace.HasException) _grid.Rows[index].DefaultCellStyle.ForeColor = Theme.Danger;
            }
            _grid.SelectionChanged += (s, e) => ShowDetail();
            split.Panel1.Controls.Add(_grid);

            _detail = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 249, 252),
                Font = Theme.Mono
            };
            split.Panel2.Controls.Add(_detail);

            Body.Controls.Add(split);
            Body.Controls.Add(note);

            AddFooterButton(Strings.Close, ButtonVariant.Primary, DialogResult.OK);
            var copy = AddFooterButton(Strings.Copy, ButtonVariant.Secondary, DialogResult.None, IconKind.Copy);
            copy.Click += (s, e) =>
            {
                if (_detail.TextLength > 0) Clipboard.SetText(_detail.Text);
            };
            if (!string.IsNullOrEmpty(listUrl))
            {
                var open = AddFooterButton(Strings.OpenTraceList, ButtonVariant.Ghost, DialogResult.None, IconKind.Link);
                open.Click += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(listUrl); }
                    catch { /* No browser available. */ }
                };
            }

            Shown += (s, e) =>
            {
                split.SplitterDistance = Math.Max(120, split.Height * 40 / 100);
                ShowDetail();
            };
        }

        private void ShowDetail()
        {
            var trace = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].Tag as TraceLogEntry : null;
            if (trace == null)
            {
                _detail.Text = string.Empty;
                return;
            }

            var builder = new StringBuilder();
            if (trace.HasException)
            {
                builder.AppendLine("=== " + Strings.TraceException + " ===").AppendLine(trace.ExceptionDetails.Trim()).AppendLine();
            }
            builder.AppendLine("=== " + Strings.TraceMessageBlock + " ===").Append(string.IsNullOrWhiteSpace(trace.MessageBlock) ? "–" : trace.MessageBlock.Trim());
            _detail.Text = builder.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        }
    }
}
