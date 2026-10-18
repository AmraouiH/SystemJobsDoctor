using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SystemJobsDoctor.Models;

namespace SystemJobsDoctor.UI.Controls
{
    /// <summary>
    /// Stacked columns of jobs per day (or week), split by status family, with a hover tooltip.
    /// </summary>
    [DesignerCategory("Code")]
    public class TimelineChart : Control
    {
        public class Series
        {
            public string Label { get; set; }
            public Color Color { get; set; }
            public Func<DayBucket, int> Value { get; set; }
        }

        private List<DayBucket> _buckets = new List<DayBucket>();
        private int _hover = -1;
        private readonly ToolTip _tip = new ToolTip { InitialDelay = 0, ReshowDelay = 0 };

        public TimelineChart()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<Series> SeriesList { get; set; } = new List<Series>();

        [DefaultValue(null)]
        public string EmptyText { get; set; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Weekly { get; set; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<DayBucket> Buckets
        {
            get => _buckets;
            set
            {
                _buckets = value ?? new List<DayBucket>();
                Weekly = _buckets.Count > 1 && (_buckets[1].Day - _buckets[0].Day).TotalDays >= 7;
                _hover = -1;
                Invalidate();
            }
        }

        private Rectangle PlotArea => new Rectangle(44, 30, Math.Max(10, Width - 56), Math.Max(10, Height - 56));

        private int IndexAt(int x)
        {
            if (_buckets.Count == 0) return -1;
            var plot = PlotArea;
            if (x < plot.Left || x > plot.Right) return -1;
            var slot = (float)plot.Width / _buckets.Count;
            var index = (int)((x - plot.Left) / slot);
            return index >= 0 && index < _buckets.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var index = IndexAt(e.X);
            if (index == _hover) return;
            _hover = index;
            Invalidate();

            if (index < 0)
            {
                _tip.Hide(this);
                return;
            }

            var bucket = _buckets[index];
            var lines = new List<string> { Weekly ? $"{bucket.Day:d} (+7)" : bucket.Day.ToString("D"), $"Total: {bucket.Total:N0}" };
            lines.AddRange(SeriesList.Select(s => new { s.Label, Value = s.Value(bucket) }).Where(s => s.Value > 0).Select(s => $"{s.Label}: {s.Value:N0}"));
            _tip.Show(string.Join(Environment.NewLine, lines), this, e.X + 14, e.Y + 14);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            _tip.Hide(this);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            // Legend
            var x = 44;
            foreach (var series in SeriesList)
            {
                using (var brush = new SolidBrush(series.Color)) g.FillRectangle(brush, x, 10, 10, 10);
                TextRenderer.DrawText(g, series.Label, Theme.Small, new Point(x + 14, 8), Theme.Muted, TextFormatFlags.NoPadding);
                x += 14 + TextRenderer.MeasureText(series.Label, Theme.Small).Width + 12;
            }

            var plot = PlotArea;
            if (_buckets.Count == 0 || _buckets.All(b => b.Total == 0))
            {
                TextRenderer.DrawText(g, EmptyText ?? string.Empty, Theme.Base, plot, Theme.Subtle,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var max = NiceMax(_buckets.Max(b => b.Total));

            // Gridlines + Y labels
            using (var grid = new Pen(Theme.Divider))
            {
                for (var i = 0; i <= 4; i++)
                {
                    var y = plot.Bottom - plot.Height * i / 4f;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    var label = (max * i / 4).ToString("N0");
                    var size = TextRenderer.MeasureText(label, Theme.Small);
                    TextRenderer.DrawText(g, label, Theme.Small, new Point(plot.Left - size.Width - 2, (int)y - size.Height / 2), Theme.Subtle, TextFormatFlags.NoPadding);
                }
            }

            var slot = (float)plot.Width / _buckets.Count;
            var barWidth = Math.Max(1f, Math.Min(28f, slot * 0.72f));

            for (var i = 0; i < _buckets.Count; i++)
            {
                var bucket = _buckets[i];
                var left = plot.Left + i * slot + (slot - barWidth) / 2;
                float bottom = plot.Bottom;

                if (i == _hover)
                {
                    using (var hover = new SolidBrush(Theme.AccentSoft)) g.FillRectangle(hover, plot.Left + i * slot, plot.Top, slot, plot.Height);
                }

                foreach (var series in SeriesList)
                {
                    var value = series.Value(bucket);
                    if (value <= 0) continue;
                    var height = plot.Height * (float)value / max;
                    using (var brush = new SolidBrush(series.Color))
                    {
                        g.FillRectangle(brush, left, bottom - height, barWidth, height);
                    }
                    bottom -= height;
                }
            }

            // X labels: first, middle and last bucket
            var indexes = new[] { 0, _buckets.Count / 2, _buckets.Count - 1 }.Distinct();
            foreach (var index in indexes)
            {
                var text = _buckets[index].Day.ToString("dd MMM");
                var size = TextRenderer.MeasureText(text, Theme.Small);
                var center = plot.Left + index * slot + slot / 2;
                var left = (int)Math.Max(plot.Left - 10, Math.Min(plot.Right - size.Width + 10, center - size.Width / 2f));
                TextRenderer.DrawText(g, text, Theme.Small, new Point(left, plot.Bottom + 6), Theme.Muted, TextFormatFlags.NoPadding);
            }
        }

        private static int NiceMax(int value)
        {
            if (value <= 4) return 4;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
            {
                var candidate = step * magnitude;
                if (candidate >= value) return (int)Math.Ceiling(candidate / 4) * 4;
            }
            return value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
