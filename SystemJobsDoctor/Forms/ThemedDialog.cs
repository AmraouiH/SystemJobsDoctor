using System.ComponentModel;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SystemJobsDoctor.UI;
using SystemJobsDoctor.UI.Controls;

namespace SystemJobsDoctor.Forms
{
    /// <summary>
    /// Base dialog with the System Jobs Doctor look: gradient header, light body, white footer with buttons.
    /// </summary>
    [DesignerCategory("Code")]
    public class ThemedDialog : Form
    {
        protected readonly Panel Body;
        protected readonly FooterPanel Footer;

        public ThemedDialog(string title, string subtitle)
        {
            Text = title;
            Font = Theme.Base;
            BackColor = Theme.Canvas;
            StartPosition = FormStartPosition.CenterParent;
            ShowIcon = false;
            ShowInTaskbar = false;
            MinimizeBox = false;

            var header = new DialogHeader { Dock = DockStyle.Top, Height = 70, Title = title, Subtitle = subtitle };

            Body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 10), BackColor = Theme.Canvas };

            var footerHost = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Theme.Card };
            footerHost.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 0, 0, footerHost.Width, 0);
            };
            Footer = new FooterPanel { Dock = DockStyle.Fill, BackColor = Theme.Card };
            footerHost.Controls.Add(Footer);

            Controls.Add(Body);
            Controls.Add(footerHost);
            Controls.Add(header);
        }

        protected ModernButton AddFooterButton(string text, ButtonVariant variant, DialogResult result, IconKind glyph = IconKind.None)
        {
            var button = new ModernButton
            {
                Text = text,
                Variant = variant,
                Glyph = glyph,
                DialogResult = result,
                AutoSize = false,
                MinimumSize = new Size(100, 34),
                Height = 34,
                Margin = new Padding(8, 0, 0, 0)
            };
            Footer.Controls.Add(button);
            return button;
        }

        /// <summary>The dialog can never be narrower than its footer buttons.</summary>
        protected override void OnLoad(System.EventArgs e)
        {
            base.OnLoad(e);
            var required = Footer.RequiredWidth + (Width - ClientSize.Width);
            // Header (70) + footer (60) + some body: the buttons can never be pushed out of the window.
            var requiredHeight = 70 + 60 + Body.Padding.Vertical + 100 + (Height - ClientSize.Height);
            if (FormBorderStyle != FormBorderStyle.Sizable)
            {
                // Fixed-size dialog: only make sure it is wide enough for its buttons.
                if (Width < required) Width = required;
                return;
            }
            MinimumSize = new Size(System.Math.Max(MinimumSize.Width, required), System.Math.Max(MinimumSize.Height, requiredHeight));
            if (Width < MinimumSize.Width) Width = MinimumSize.Width;
            if (Height < MinimumSize.Height) Height = MinimumSize.Height;
        }

        /// <summary>
        /// Footer laid out by hand (a right-to-left FlowLayoutPanel misplaces auto-sized buttons):
        /// buttons from the right edge in the order they were added, other controls from the left edge,
        /// fixed margins, vertically centered, never overlapping.
        /// </summary>
        [DesignerCategory("Code")]
        protected class FooterPanel : Panel
        {
            public const int EdgeMargin = 16;
            public const int Gap = 8;

            protected override void OnControlAdded(ControlEventArgs e)
            {
                base.OnControlAdded(e);
                e.Control.TextChanged += (s, args) => PerformLayout();
                e.Control.VisibleChanged += (s, args) => PerformLayout();
            }

            private static Size SizeOf(Control control)
            {
                var preferred = control.GetPreferredSize(Size.Empty);
                return new Size(System.Math.Max(control.MinimumSize.Width, preferred.Width), System.Math.Max(control.MinimumSize.Height, control is Button ? 34 : preferred.Height));
            }

            /// <summary>Width needed to show every control with its margins.</summary>
            public int RequiredWidth
            {
                get
                {
                    var controls = Controls.Cast<Control>().Where(c => c.Visible).ToList();
                    return 2 * EdgeMargin + controls.Sum(c => SizeOf(c).Width) + System.Math.Max(0, controls.Count - 1) * Gap + (controls.Any(c => !(c is Button)) ? 16 : 0);
                }
            }

            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                var right = ClientSize.Width - EdgeMargin;
                var left = EdgeMargin;
                foreach (Control control in Controls)
                {
                    if (!control.Visible) continue;
                    var size = SizeOf(control);
                    var top = (ClientSize.Height - size.Height) / 2;
                    if (control is Button)
                    {
                        control.Bounds = new Rectangle(right - size.Width, top, size.Width, size.Height);
                        right -= size.Width + Gap;
                    }
                    else
                    {
                        control.Bounds = new Rectangle(left, top, size.Width, size.Height);
                        left += size.Width + Gap;
                    }
                }
            }
        }

        /// <summary>
        /// Gradient header of the dialogs.
        /// </summary>
        [DesignerCategory("Code")]
        private class DialogHeader : Control
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }

            public DialogHeader()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                var bounds = ClientRectangle;
                if (bounds.Width <= 0) return;

                using (var brush = new LinearGradientBrush(bounds, Theme.HeaderStart, Theme.HeaderEnd, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brush, bounds);
                }

                g.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.DrawPulseDecoration(g, bounds);

                using (var logo = Theme.AhLogo(34))
                {
                    g.DrawImage(logo, 18, (Height - 34) / 2);
                }
                TextRenderer.DrawText(g, Title, Theme.HeaderTitle, new Point(62, 14), Color.White, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Subtitle, Theme.Base, new Rectangle(63, 42, Width - 80, 18), Color.FromArgb(176, 214, 230),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                var line = new Rectangle(0, Height - 3, Width, 3);
                using (var accent = new LinearGradientBrush(line, Theme.Teal, Theme.Accent, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(accent, line);
                }
            }
        }
    }
}
