using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.Models;
using SystemJobsDoctor.UI;
using SystemJobsDoctor.UI.Controls;

namespace SystemJobsDoctor.Forms
{
    /// <summary>
    /// Confirmation of a bulk action: eligible / skipped counts, the most impacted processes,
    /// and an explicit acknowledgement for deletions.
    /// </summary>
    [DesignerCategory("Code")]
    public class ActionConfirmForm : ThemedDialog
    {
        private readonly ToolTip toolTip = new ToolTip { AutoPopDelay = 15000 };

        public ActionConfirmForm(JobActionPlan plan)
            : base(Strings.ActionTitle(plan.Kind), Strings.ActionSubtitle(plan.Kind))
        {
            ClientSize = new Size(760, 470);
            // Three tiles side by side + the list of processes.
            MinimumSize = new Size(720, 520);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;

            var tiles = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 76, WrapContents = false, BackColor = Theme.Canvas };
            var tint = plan.Kind == JobActionKind.Delete ? Theme.Danger : plan.Kind == JobActionKind.Resume ? Theme.Success : Theme.Accent;
            tiles.Controls.Add(new StatTile { Value = plan.Eligible.Count.ToString("N0"), Caption = Strings.TileEligible(plan.Kind), Tint = tint, Size = new Size(200, 64), Margin = new Padding(0, 0, 10, 0) });
            tiles.Controls.Add(new StatTile { Value = plan.Skipped.Count.ToString("N0"), Caption = Strings.TileSkipped, Tint = Theme.Subtle, Size = new Size(200, 64), Margin = new Padding(0, 0, 10, 0) });
            if (plan.Kind == JobActionKind.Resume)
            {
                var failed = plan.Eligible.Count(j => j.StatusCode == JobStatus.Failed);
                if (failed > 0) tiles.Controls.Add(new StatTile { Value = failed.ToString("N0"), Caption = Strings.TileFailedToResume, Tint = Theme.Danger, Size = new Size(200, 64) });
            }

            var rule = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                ForeColor = Theme.Muted,
                Text = Strings.ActionRule(plan.Kind) + (plan.PostponeUntilUtc.HasValue ? "\r\n" + Strings.PostponeTo(plan.PostponeUntilUtc.Value.ToLocalTime()) : string.Empty)
            };

            var caption = new Label { Dock = DockStyle.Top, Height = 22, Font = Theme.SmallBold, ForeColor = Theme.Muted, Text = Strings.ImpactedProcesses, TextAlign = ContentAlignment.BottomLeft };

            var list = new BarList { Dock = DockStyle.Fill, Clickable = false };
            list.Items = plan.Eligible
                .GroupBy(j => j.ProcessKey)
                .Select(g => new Models.BreakdownItem { Key = g.Key, Label = g.First().ProcessName, Count = g.Count(), Color = tint })
                .OrderByDescending(i => i.Count)
                .Take(8)
                .ToList();
            var listHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(8) };
            listHost.Controls.Add(list);

            // Options are shown in the body (full width), above the list of processes.
            CheckBox option = null;
            if (plan.Kind == JobActionKind.Resume && plan.Eligible.Any(j => j.StatusCode == JobStatus.Failed && j.ProcessKind == ProcessKind.Workflow))
            {
                option = OptionBox(Strings.RerunOption, Theme.Text);
                option.Checked = plan.RerunFailedWorkflows;
                option.CheckedChanged += (s, e) => plan.RerunFailedWorkflows = option.Checked;
                toolTip.SetToolTip(option, Strings.RerunOptionTip);
            }
            else if (plan.Kind == JobActionKind.Delete)
            {
                option = OptionBox(Strings.DeleteAcknowledge, Theme.Danger);
            }

            Body.Controls.Add(listHost);
            Body.Controls.Add(caption);
            if (option != null)
            {
                var optionHost = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.Canvas };
                optionHost.Controls.Add(option);
                Body.Controls.Add(optionHost);
            }
            Body.Controls.Add(rule);
            Body.Controls.Add(tiles);

            AddFooterButton(Strings.CancelButton, ButtonVariant.Secondary, DialogResult.Cancel);
            var ok = AddFooterButton(Strings.ActionButton(plan.Kind, plan.Eligible.Count), ButtonVariant.Primary, DialogResult.OK, Strings.ActionGlyph(plan.Kind));
            AcceptButton = ok;
            CancelButton = Footer.Controls[0] as IButtonControl;

            if (plan.Kind == JobActionKind.Delete)
            {
                ok.Enabled = false;
                option.CheckedChanged += (s, e) => ok.Enabled = option.Checked;
            }
        }

        private static CheckBox OptionBox(string text, Color color) => new CheckBox
        {
            Text = text,
            AutoSize = true,
            Font = Theme.Semibold,
            ForeColor = color,
            Location = new Point(2, 8)
        };
    }
}
