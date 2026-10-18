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
    /// Choice of the new « postpone until » date of the selected jobs.
    /// </summary>
    [DesignerCategory("Code")]
    public class PostponeForm : ThemedDialog
    {
        private readonly DateTimePicker _picker;

        public DateTime SelectedDate => _picker.Value;

        public PostponeForm(int count)
            : base(Strings.PostponeTitle, Strings.PostponeSubtitle(count))
        {
            ClientSize = new Size(520, 270);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, BackColor = Theme.Canvas };
            row.Controls.Add(new Label { Text = Strings.PostponeUntilLabel, AutoSize = true, Font = Theme.Semibold, ForeColor = Theme.Muted, Margin = new Padding(0, 10, 8, 0) });
            _picker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm",
                Width = 150,
                Value = DateTime.Today.AddHours(22) > DateTime.Now ? DateTime.Today.AddHours(22) : DateTime.Today.AddDays(1).AddHours(22),
                Margin = new Padding(0, 6, 0, 0)
            };
            row.Controls.Add(_picker);

            var presets = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, BackColor = Theme.Canvas };
            AddPreset(presets, Strings.PresetOneHour, () => DateTime.Now.AddHours(1));
            AddPreset(presets, Strings.PresetTonight, () => DateTime.Today.AddHours(22) > DateTime.Now ? DateTime.Today.AddHours(22) : DateTime.Today.AddDays(1).AddHours(22));
            AddPreset(presets, Strings.PresetTomorrow, () => DateTime.Today.AddDays(1).AddHours(6));
            AddPreset(presets, Strings.PresetWeekend, () =>
            {
                var days = ((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7;
                return DateTime.Today.AddDays(days == 0 ? 7 : days).AddHours(2);
            });

            var note = new Label { Dock = DockStyle.Top, Height = 46, ForeColor = Theme.Muted, Text = Strings.PostponeNote };

            Body.Controls.Add(note);
            Body.Controls.Add(presets);
            Body.Controls.Add(row);

            AddFooterButton(Strings.CancelButton, ButtonVariant.Secondary, DialogResult.Cancel);
            var ok = AddFooterButton(Strings.Continue, ButtonVariant.Primary, DialogResult.OK, IconKind.Clock);
            ok.Click += (s, e) =>
            {
                if (_picker.Value > DateTime.Now) return;
                MessageBox.Show(this, Strings.PostponeInPast, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            };
            AcceptButton = ok;
        }

        private void AddPreset(FlowLayoutPanel host, string text, Func<DateTime> value)
        {
            var button = new ModernButton { Text = text, Variant = ButtonVariant.Secondary, AutoSize = true, MinimumSize = new Size(80, 30), Height = 30, Margin = new Padding(0, 4, 8, 0) };
            button.Click += (s, e) => _picker.Value = value();
            host.Controls.Add(button);
        }
    }
}
