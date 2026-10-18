using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SystemJobsDoctor.UI.Controls
{
    /// <summary>
    /// Flat pill tabs (icon, text, optional counter badge). The host shows the page matching <see cref="SelectedIndex"/>.
    /// </summary>
    [DesignerCategory("Code")]
    public class SegmentedTabs : Control
    {
        public class Tab
        {
            public string Text { get; set; }
            public IconKind Glyph { get; set; }
            public string Badge { get; set; }
            public Color BadgeColor { get; set; } = Theme.Danger;
            internal Rectangle Bounds;
        }

        private int _selected;
        private int _hover = -1;

        public event EventHandler SelectedIndexChanged;

        public SegmentedTabs()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 44;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<Tab> Tabs { get; } = new List<Tab>();

        [DefaultValue(0)]
        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value < 0 || value >= Tabs.Count || value == _selected) return;
                _selected = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ArrangeTabs(Graphics g)
        {
            var x = 0;
            foreach (var tab in Tabs)
            {
                var text = TextRenderer.MeasureText(g, tab.Text ?? string.Empty, Theme.Semibold, Size.Empty, TextFormatFlags.NoPadding).Width;
                var badge = string.IsNullOrEmpty(tab.Badge) ? 0 : TextRenderer.MeasureText(g, tab.Badge, Theme.SmallBold, Size.Empty, TextFormatFlags.NoPadding).Width + 16;
                var width = 18 + 22 + text + badge + 16;
                tab.Bounds = new Rectangle(x, 4, width, Height - 8);
                x += width + 6;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = Tabs.FindIndex(t => t.Bounds.Contains(e.Location));
            if (index == _hover) return;
            _hover = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            var index = Tabs.FindIndex(t => t.Bounds.Contains(e.Location));
            if (index >= 0) SelectedIndex = index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Canvas);
            ArrangeTabs(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (var i = 0; i < Tabs.Count; i++)
            {
                var tab = Tabs[i];
                var selected = i == _selected;
                var bounds = tab.Bounds;

                if (selected || i == _hover)
                {
                    using (var path = Theme.RoundedRect(new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1, bounds.Height - 1), 8))
                    using (var fill = new SolidBrush(selected ? Theme.Card : Color.FromArgb(234, 238, 244)))
                    {
                        g.FillPath(fill, path);
                        if (selected)
                        {
                            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, path);
                        }
                    }
                }

                var fore = selected ? Theme.Accent : Theme.Muted;
                LineIcons.Draw(g, tab.Glyph, new RectangleF(bounds.X + 14, bounds.Y + (bounds.Height - 16) / 2f, 16, 16), fore);
                var textX = bounds.X + 14 + 22;
                TextRenderer.DrawText(g, tab.Text, Theme.Semibold, new Rectangle(textX, bounds.Y, bounds.Width, bounds.Height), selected ? Theme.Text : Theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

                if (!string.IsNullOrEmpty(tab.Badge))
                {
                    var textWidth = TextRenderer.MeasureText(g, tab.Text ?? string.Empty, Theme.Semibold, Size.Empty, TextFormatFlags.NoPadding).Width;
                    var badgeWidth = TextRenderer.MeasureText(g, tab.Badge, Theme.SmallBold, Size.Empty, TextFormatFlags.NoPadding).Width + 12;
                    var badge = new RectangleF(textX + textWidth + 8, bounds.Y + (bounds.Height - 18) / 2f, badgeWidth, 18);
                    using (var path = Theme.RoundedRect(badge, 9))
                    using (var fill = new SolidBrush(Theme.Blend(Color.White, tab.BadgeColor, 0.14f)))
                    {
                        g.FillPath(fill, path);
                    }
                    TextRenderer.DrawText(g, tab.Badge, Theme.SmallBold, Rectangle.Round(badge), tab.BadgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            // Selected underline accent
            if (_selected >= 0 && _selected < Tabs.Count)
            {
                var b = Tabs[_selected].Bounds;
                using (var brush = new SolidBrush(Theme.Accent)) g.FillRectangle(brush, b.X + 14, b.Bottom - 3, b.Width - 28, 2);
            }
        }
    }
}
