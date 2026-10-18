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
    /// Read-only text (FetchXML preview…) with a copy button.
    /// </summary>
    [DesignerCategory("Code")]
    public class TextViewerForm : ThemedDialog
    {
        public TextViewerForm(string title, string subtitle, string text, bool canSave = false)
            : base(title, subtitle)
        {
            ClientSize = canSave ? new Size(940, 680) : new Size(760, 520);
            MinimumSize = new Size(500, 360);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                // Summaries are read as prose (wrapped); XML keeps its lines.
                ScrollBars = canSave ? ScrollBars.Vertical : ScrollBars.Both,
                WordWrap = canSave,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = Theme.Mono,
                Text = (text ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
            host.Controls.Add(box);
            Body.Controls.Add(host);

            AddFooterButton(Strings.Close, canSave ? ButtonVariant.Secondary : ButtonVariant.Primary, DialogResult.OK);
            if (canSave)
            {
                var save = AddFooterButton(Strings.SaveAs, ButtonVariant.Secondary, DialogResult.None, IconKind.Export);
                save.Click += (s, e) =>
                {
                    using (var dialog = new SaveFileDialog { Filter = Strings.SummaryFileFilter, FileName = $"SystemJobsDoctor_job_{DateTime.Now:yyyyMMdd_HHmm}.txt" })
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK) System.IO.File.WriteAllText(dialog.FileName, box.Text, new System.Text.UTF8Encoding(true));
                    }
                };
            }
            var copy = AddFooterButton(Strings.Copy, canSave ? ButtonVariant.Primary : ButtonVariant.Secondary, DialogResult.None, IconKind.Copy);
            copy.Click += (s, e) =>
            {
                if (box.TextLength == 0) return;
                Clipboard.SetText(box.Text);
                copy.Text = Strings.Copied;
            };
            Shown += (s, e) => box.Select(0, 0);
        }
    }
}
