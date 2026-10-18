using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SystemJobsDoctor.UI
{
    public enum IconKind
    {
        None,
        Close,
        Reload,
        Settings,
        Globe,
        Swap,
        Compare,
        Merge,
        Search,
        Load,
        Check,
        Fields,
        Heart,
        Contact,
        Play,
        Stop,
        Clock,
        Trash,
        Export,
        Chart,
        Alert,
        List,
        Database,
        Link,
        Log,
        Copy,
        Filter
    }

    /// <summary>
    /// Line icons drawn in code (no image resources), tinted to any color.
    /// </summary>
    internal static class LineIcons
    {
        public static Bitmap ToBitmap(IconKind kind, int size, Color color)
        {
            var bitmap = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bitmap))
            {
                Draw(g, kind, new RectangleF(0, 0, size, size), color);
            }
            return bitmap;
        }

        public static void Draw(Graphics g, IconKind kind, RectangleF r, Color color)
        {
            if (kind == IconKind.None) return;

            var previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var s = r.Width;

            PointF P(float x, float y) => new PointF(r.X + x * s, r.Y + y * s);

            using (var pen = new Pen(color, Math.Max(1.4f, s / 10.5f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                switch (kind)
                {
                    case IconKind.Close:
                        g.DrawLine(pen, P(0.27f, 0.27f), P(0.73f, 0.73f));
                        g.DrawLine(pen, P(0.73f, 0.27f), P(0.27f, 0.73f));
                        break;

                    case IconKind.Reload:
                    {
                        var arc = new RectangleF(r.X + 0.2f * s, r.Y + 0.2f * s, 0.6f * s, 0.6f * s);
                        g.DrawArc(pen, arc, 40, 290);
                        // Arrow head at the end of the arc (angle 330°)
                        var angle = 330 * Math.PI / 180;
                        var end = new PointF(arc.X + arc.Width / 2 + (float)Math.Cos(angle) * arc.Width / 2,
                                             arc.Y + arc.Height / 2 + (float)Math.Sin(angle) * arc.Height / 2);
                        g.FillPolygon(brush, new[]
                        {
                            new PointF(end.X + 0.14f * s, end.Y - 0.02f * s),
                            new PointF(end.X - 0.08f * s, end.Y - 0.12f * s),
                            new PointF(end.X - 0.02f * s, end.Y + 0.14f * s)
                        });
                        break;
                    }

                    case IconKind.Settings:
                    {
                        var center = P(0.5f, 0.5f);
                        for (var i = 0; i < 8; i++)
                        {
                            var a = i * Math.PI / 4;
                            g.DrawLine(pen,
                                new PointF(center.X + (float)Math.Cos(a) * 0.26f * s, center.Y + (float)Math.Sin(a) * 0.26f * s),
                                new PointF(center.X + (float)Math.Cos(a) * 0.40f * s, center.Y + (float)Math.Sin(a) * 0.40f * s));
                        }
                        g.DrawEllipse(pen, r.X + 0.24f * s, r.Y + 0.24f * s, 0.52f * s, 0.52f * s);
                        g.DrawEllipse(pen, r.X + 0.41f * s, r.Y + 0.41f * s, 0.18f * s, 0.18f * s);
                        break;
                    }

                    case IconKind.Globe:
                        g.DrawEllipse(pen, r.X + 0.14f * s, r.Y + 0.14f * s, 0.72f * s, 0.72f * s);
                        g.DrawEllipse(pen, r.X + 0.34f * s, r.Y + 0.14f * s, 0.32f * s, 0.72f * s);
                        g.DrawLine(pen, P(0.16f, 0.5f), P(0.84f, 0.5f));
                        break;

                    case IconKind.Swap:
                        Arrow(g, pen, brush, P(0.18f, 0.34f), P(0.82f, 0.34f), s);
                        Arrow(g, pen, brush, P(0.82f, 0.66f), P(0.18f, 0.66f), s);
                        break;

                    case IconKind.Compare:
                        using (var p1 = Theme.RoundedRect(new RectangleF(r.X + 0.14f * s, r.Y + 0.18f * s, 0.30f * s, 0.64f * s), 0.06f * s))
                        using (var p2 = Theme.RoundedRect(new RectangleF(r.X + 0.56f * s, r.Y + 0.18f * s, 0.30f * s, 0.64f * s), 0.06f * s))
                        {
                            g.DrawPath(pen, p1);
                            g.DrawPath(pen, p2);
                        }
                        break;

                    case IconKind.Merge:
                        g.DrawLine(pen, P(0.24f, 0.18f), P(0.5f, 0.46f));
                        g.DrawLine(pen, P(0.76f, 0.18f), P(0.5f, 0.46f));
                        Arrow(g, pen, brush, P(0.5f, 0.46f), P(0.5f, 0.86f), s);
                        break;

                    case IconKind.Search:
                        g.DrawEllipse(pen, r.X + 0.16f * s, r.Y + 0.16f * s, 0.46f * s, 0.46f * s);
                        g.DrawLine(pen, P(0.57f, 0.57f), P(0.84f, 0.84f));
                        break;

                    case IconKind.Load:
                        Arrow(g, pen, brush, P(0.5f, 0.14f), P(0.5f, 0.66f), s);
                        g.DrawLine(pen, P(0.2f, 0.84f), P(0.8f, 0.84f));
                        break;

                    case IconKind.Check:
                        g.DrawLines(pen, new[] { P(0.22f, 0.52f), P(0.42f, 0.72f), P(0.8f, 0.3f) });
                        break;

                    case IconKind.Heart:
                        using (var heart = new GraphicsPath())
                        {
                            heart.AddBezier(P(0.5f, 0.84f), P(0.12f, 0.58f), P(0.1f, 0.3f), P(0.3f, 0.2f));
                            heart.AddBezier(P(0.3f, 0.2f), P(0.42f, 0.15f), P(0.5f, 0.24f), P(0.5f, 0.32f));
                            heart.AddBezier(P(0.5f, 0.32f), P(0.5f, 0.24f), P(0.58f, 0.15f), P(0.7f, 0.2f));
                            heart.AddBezier(P(0.7f, 0.2f), P(0.9f, 0.3f), P(0.88f, 0.58f), P(0.5f, 0.84f));
                            heart.CloseFigure();
                            g.FillPath(brush, heart);
                        }
                        break;

                    case IconKind.Contact:
                        g.DrawEllipse(pen, r.X + 0.34f * s, r.Y + 0.14f * s, 0.32f * s, 0.32f * s);
                        g.DrawArc(pen, r.X + 0.18f * s, r.Y + 0.54f * s, 0.64f * s, 0.6f * s, 180, 180);
                        break;

                    case IconKind.Play:
                        g.FillPolygon(brush, new[] { P(0.32f, 0.2f), P(0.8f, 0.5f), P(0.32f, 0.8f) });
                        break;

                    case IconKind.Stop:
                        g.DrawEllipse(pen, r.X + 0.14f * s, r.Y + 0.14f * s, 0.72f * s, 0.72f * s);
                        g.DrawLine(pen, P(0.26f, 0.74f), P(0.74f, 0.26f));
                        break;

                    case IconKind.Clock:
                        g.DrawEllipse(pen, r.X + 0.14f * s, r.Y + 0.14f * s, 0.72f * s, 0.72f * s);
                        g.DrawLines(pen, new[] { P(0.5f, 0.3f), P(0.5f, 0.5f), P(0.65f, 0.6f) });
                        break;

                    case IconKind.Trash:
                        g.DrawLine(pen, P(0.18f, 0.28f), P(0.82f, 0.28f));
                        g.DrawLines(pen, new[] { P(0.4f, 0.28f), P(0.42f, 0.16f), P(0.58f, 0.16f), P(0.6f, 0.28f) });
                        g.DrawLines(pen, new[] { P(0.26f, 0.32f), P(0.31f, 0.84f), P(0.69f, 0.84f), P(0.74f, 0.32f) });
                        break;

                    case IconKind.Export:
                        g.DrawLines(pen, new[] { P(0.5f, 0.2f), P(0.2f, 0.2f), P(0.2f, 0.8f), P(0.8f, 0.8f), P(0.8f, 0.5f) });
                        Arrow(g, pen, brush, P(0.45f, 0.55f), P(0.84f, 0.16f), s);
                        break;

                    case IconKind.Chart:
                        g.DrawLine(pen, P(0.24f, 0.8f), P(0.24f, 0.5f));
                        g.DrawLine(pen, P(0.5f, 0.8f), P(0.5f, 0.22f));
                        g.DrawLine(pen, P(0.76f, 0.8f), P(0.76f, 0.4f));
                        break;

                    case IconKind.Alert:
                        g.DrawPolygon(pen, new[] { P(0.5f, 0.14f), P(0.88f, 0.82f), P(0.12f, 0.82f) });
                        g.DrawLine(pen, P(0.5f, 0.4f), P(0.5f, 0.58f));
                        g.FillEllipse(brush, r.X + 0.45f * s, r.Y + 0.66f * s, 0.1f * s, 0.1f * s);
                        break;

                    case IconKind.List:
                        for (var i = 0; i < 3; i++)
                        {
                            var y = 0.28f + i * 0.22f;
                            g.FillEllipse(brush, r.X + 0.15f * s, r.Y + (y - 0.05f) * s, 0.1f * s, 0.1f * s);
                            g.DrawLine(pen, P(0.36f, y), P(0.84f, y));
                        }
                        break;

                    case IconKind.Database:
                        g.DrawEllipse(pen, r.X + 0.2f * s, r.Y + 0.14f * s, 0.6f * s, 0.2f * s);
                        g.DrawLine(pen, P(0.2f, 0.24f), P(0.2f, 0.76f));
                        g.DrawLine(pen, P(0.8f, 0.24f), P(0.8f, 0.76f));
                        g.DrawArc(pen, r.X + 0.2f * s, r.Y + 0.4f * s, 0.6f * s, 0.2f * s, 0, 180);
                        g.DrawArc(pen, r.X + 0.2f * s, r.Y + 0.66f * s, 0.6f * s, 0.2f * s, 0, 180);
                        break;

                    case IconKind.Link:
                        g.DrawLines(pen, new[] { P(0.56f, 0.18f), P(0.82f, 0.18f), P(0.82f, 0.44f) });
                        g.DrawLine(pen, P(0.82f, 0.18f), P(0.46f, 0.54f));
                        g.DrawLines(pen, new[] { P(0.7f, 0.6f), P(0.7f, 0.82f), P(0.18f, 0.82f), P(0.18f, 0.3f), P(0.4f, 0.3f) });
                        break;

                    case IconKind.Log:
                        using (var page = Theme.RoundedRect(new RectangleF(r.X + 0.22f * s, r.Y + 0.12f * s, 0.56f * s, 0.76f * s), 0.06f * s))
                        {
                            g.DrawPath(pen, page);
                        }
                        g.DrawLine(pen, P(0.34f, 0.36f), P(0.66f, 0.36f));
                        g.DrawLine(pen, P(0.34f, 0.52f), P(0.66f, 0.52f));
                        g.DrawLine(pen, P(0.34f, 0.68f), P(0.54f, 0.68f));
                        break;

                    case IconKind.Copy:
                        using (var back = Theme.RoundedRect(new RectangleF(r.X + 0.16f * s, r.Y + 0.16f * s, 0.46f * s, 0.5f * s), 0.06f * s))
                        using (var front = Theme.RoundedRect(new RectangleF(r.X + 0.38f * s, r.Y + 0.34f * s, 0.46f * s, 0.5f * s), 0.06f * s))
                        {
                            g.DrawPath(pen, back);
                            g.DrawPath(pen, front);
                        }
                        break;

                    case IconKind.Filter:
                        g.DrawPolygon(pen, new[] { P(0.16f, 0.2f), P(0.84f, 0.2f), P(0.58f, 0.52f), P(0.58f, 0.8f), P(0.42f, 0.72f), P(0.42f, 0.52f) });
                        break;

                    case IconKind.Fields:
                        g.DrawLine(pen, P(0.2f, 0.3f), P(0.8f, 0.3f));
                        g.DrawLine(pen, P(0.2f, 0.5f), P(0.8f, 0.5f));
                        g.DrawLine(pen, P(0.2f, 0.7f), P(0.6f, 0.7f));
                        break;
                }
            }

            g.SmoothingMode = previous;
        }

        private static void Arrow(Graphics g, Pen pen, Brush brush, PointF from, PointF to, float size)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return;
            var ux = dx / length;
            var uy = dy / length;
            var head = 0.2f * size;

            g.DrawLine(pen, from, new PointF(to.X - ux * head * 0.6f, to.Y - uy * head * 0.6f));
            g.FillPolygon(brush, new[]
            {
                to,
                new PointF(to.X - ux * head - uy * head * 0.6f, to.Y - uy * head + ux * head * 0.6f),
                new PointF(to.X - ux * head + uy * head * 0.6f, to.Y - uy * head - ux * head * 0.6f)
            });
        }
    }
}
