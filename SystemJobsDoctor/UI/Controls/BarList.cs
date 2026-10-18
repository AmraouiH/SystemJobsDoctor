using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.UI.Controls
{
    /// <summary>
    /// Ranked horizontal bars (label, count, share), scrollable and clickable.
    /// </summary>
    [DesignerCategory("Code")]
    public class BarList : Panel
    {
        private const int RowHeight = 38;
        private List<BreakdownItem> _items = new List<BreakdownItem>();
        private int _hover = -1;
        private string _emptyText = "–";

        public event EventHandler<BreakdownItem> ItemClicked;

        public BarList()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            AutoScroll = true;
        }

        [DefaultValue("–")]
        public string EmptyText
        {
            get => _emptyText;
            set { _emptyText = value; Invalidate(); }
        }

        /// <summary>Show a hand cursor and raise <see cref="ItemClicked"/>.</summary>
        [DefaultValue(true)]
        public bool Clickable { get; set; } = true;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<BreakdownItem> Items
        {
            get => _items;
            set
            {
                _items = value ?? new List<BreakdownItem>();
                _hover = -1;
                AutoScrollMinSize = new Size(0, _items.Count * RowHeight + 4);
                AutoScrollPosition = Point.Empty;
                Invalidate();
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate();
        }

        private int IndexAt(Point location)
        {
            var y = location.Y - AutoScrollPosition.Y;
            var index = y / RowHeight;
            return index >= 0 && index < _items.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = IndexAt(e.Location);
            if (index == _hover) return;
            _hover = index;
            Cursor = Clickable && index >= 0 ? Cursors.Hand : Cursors.Default;
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
            if (!Clickable || e.Button != MouseButtons.Left) return;
            var index = IndexAt(e.Location);
            if (index >= 0) ItemClicked?.Invoke(this, _items[index]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            if (_items.Count == 0)
            {
                TextRenderer.DrawText(g, _emptyText, Theme.Base, ClientRectangle, Theme.Subtle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            var max = Math.Max(1, _items.Max(i => i.Count));
            var total = Math.Max(1, _items.Sum(i => i.Count));
            var width = ClientSize.Width - 8;
            var offset = AutoScrollPosition.Y;
            var first = Math.Max(0, -offset / RowHeight);
            var last = Math.Min(_items.Count - 1, (-offset + ClientSize.Height) / RowHeight);

            for (var i = first; i <= last; i++)
            {
                var item = _items[i];
                var top = i * RowHeight + offset;
                var color = item.Color.IsEmpty ? Theme.Accent : item.Color;

                if (i == _hover && Clickable)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var path = Theme.RoundedRect(new RectangleF(1, top + 1, width + 4, RowHeight - 2), 6))
                    using (var brush = new SolidBrush(Theme.Blend(Color.White, color, 0.07f)))
                    {
                        g.FillPath(brush, path);
                    }
                }

                var countText = item.Count.ToString("N0");
                var share = $"{100.0 * item.Count / total:0.#}%";
                var countSize = TextRenderer.MeasureText(g, countText, Theme.Semibold, Size.Empty, TextFormatFlags.NoPadding);
                var shareSize = TextRenderer.MeasureText(g, share, Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
                var right = width;

                TextRenderer.DrawText(g, share, Theme.Small, new Point(right - shareSize.Width, top + 7), Theme.Subtle, TextFormatFlags.NoPadding);
                right -= shareSize.Width + 8;
                TextRenderer.DrawText(g, countText, Theme.Semibold, new Point(right - countSize.Width, top + 5), Theme.Text, TextFormatFlags.NoPadding);
                right -= countSize.Width + 10;

                var label = item.Label ?? string.Empty;
                var labelRect = new Rectangle(6, top + 5, Math.Max(10, right - 6), 18);
                TextRenderer.DrawText(g, label, Theme.Base, labelRect, Theme.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                // Track + bar
                var track = new RectangleF(6, top + 26, width - 6, 5);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var trackPath = Theme.RoundedRect(track, 2.5f))
                using (var trackBrush = new SolidBrush(Theme.Divider))
                {
                    g.FillPath(trackBrush, trackPath);
                }
                var barWidth = Math.Max(4f, track.Width * item.Count / max);
                using (var barPath = Theme.RoundedRect(new RectangleF(track.X, track.Y, barWidth, track.Height), 2.5f))
                using (var barBrush = new SolidBrush(color))
                {
                    g.FillPath(barBrush, barPath);
                }
                g.SmoothingMode = SmoothingMode.None;
            }
        }
    }
}
