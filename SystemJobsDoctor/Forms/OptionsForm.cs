using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using SystemJobsDoctor.Localization;
using SystemJobsDoctor.UI;
using SystemJobsDoctor.UI.Controls;

namespace SystemJobsDoctor.Forms
{
    /// <summary>
    /// Tool options: analysis limit, batch size of the bulk actions, average job size.
    /// </summary>
    [DesignerCategory("Code")]
    public class OptionsForm : ThemedDialog
    {
        public OptionsForm(PluginSettings settings)
            : base(Strings.Options, Strings.OptionsSubtitle)
        {
            ClientSize = new Size(600, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            // Docked Top: added from the last row to the first.
            var parallel = AddRow(Strings.OptionParallel, Strings.OptionParallelHelp, 1, 8, settings.ParallelQueries, 1);
            var size = AddRow(Strings.AvgKbLabel, Strings.AvgKbTip, 1, 2000, settings.AverageJobSizeKb, 1);
            var batch = AddRow(Strings.OptionBatchSize, Strings.OptionBatchSizeHelp, 1, 1000, settings.BatchSize, 50);
            var max = AddRow(Strings.OptionMaxRecords, Strings.OptionMaxRecordsHelp, 100, 500000, settings.MaxRecords, 5000);

            AddFooterButton(Strings.CancelButton, ButtonVariant.Secondary, DialogResult.Cancel);
            var ok = AddFooterButton(Strings.Save, ButtonVariant.Primary, DialogResult.OK, IconKind.Check);
            ok.Click += (s, e) =>
            {
                settings.MaxRecords = (int)max.Value;
                settings.BatchSize = (int)batch.Value;
                settings.AverageJobSizeKb = (int)size.Value;
                settings.ParallelQueries = (int)parallel.Value;
            };
            AcceptButton = ok;
        }

        private NumericUpDown AddRow(string label, string help, int min, int max, int value, int increment)
        {
            // Description docked (it wraps), number and caption at fixed positions in the left / top padding.
            var row = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Canvas, Padding = new Padding(118, 24, 0, 6) };
            var number = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Increment = increment,
                Value = Math.Max(min, Math.Min(max, value)),
                ThousandsSeparator = true,
                Location = new Point(0, 4),
                Width = 100
            };
            var caption = new Label { Text = label, Font = Theme.Semibold, ForeColor = Theme.Text, AutoSize = true, Location = new Point(118, 4) };
            var description = new Label
            {
                Text = help,
                ForeColor = Theme.Muted,
                Font = Theme.Small,
                Dock = DockStyle.Fill
            };
            row.Controls.AddRange(new Control[] { number, caption, description });
            Body.Controls.Add(row);
            return number;
        }
    }
}
