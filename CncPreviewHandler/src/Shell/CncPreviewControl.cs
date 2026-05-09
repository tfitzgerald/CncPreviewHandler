using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using CncPreviewHandler.Parser;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    // ── Outer container shown while background parse runs ────────────────────
    public class CncPreviewControl : PreviewHandlerControl
    {
        public CncPreviewControl(string filePath)
        {
            BackColor = Color.FromArgb(20, 20, 20);

            var lbl = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(160, 160, 160),
                BackColor = Color.FromArgb(20, 20, 20),
                Font      = new Font("Segoe UI", 11f),
                Text      = string.IsNullOrEmpty(filePath)
                            ? "No file path received from Explorer"
                            : "Parsing toolpath\u2026"
            };
            Controls.Add(lbl);
            if (string.IsNullOrEmpty(filePath)) return;

            Task.Run(() =>
            {
                try
                {
                    var segs = new GCodeParser().Parse(filePath);
                    Invoke((Action)(() =>
                    {
                        Controls.Clear();
                        if (segs.Count == 0)
                        {
                            lbl.Text      = "No toolpath moves found in file";
                            lbl.ForeColor = Color.OrangeRed;
                            Controls.Add(lbl);
                            return;
                        }
                        var vp = new ToolpathViewport(segs) { Dock = DockStyle.Fill };
                        Controls.Add(vp);
                        vp.Focus();
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        lbl.ForeColor = Color.OrangeRed;
                        lbl.Text      = "Error: " + ex.Message;
                    }));
                }
            });
        }
    }

    // ── Pure GDI+ interactive 3-D viewport ───────────────────────────────────
    // Left-drag : orbit    Right-drag : pan    Scroll : zoom    Dbl-click : reset
    sealed class ToolpathViewport : Control
    {
        private readonly List<ToolpathSegment> _segs;
        private float _cx, _cy, _cz, _bbSize;

        private float _yaw   = -45f;
        private float _pitch =  30f;
        private float _zoom  =   1f;
        private float _panX, _panY;

        private Point _lastMouse;
        private bool  _leftDown, _rightDown;

        private readonly Pen  _rapidPen = new Pen(Color.FromArgb(70,  130, 220), 1f);
        private readonly Pen  _cutPen   = new Pen(Color.FromArgb(220,  90,  40), 1f);
        private readonly Pen  _arcPen   = new Pen(Color.FromArgb( 60, 180,  80), 1f);
        private readonly Font _uiFont   = new Font("Segoe UI", 8f);

        public ToolpathViewport(List<ToolpathSegment> segs)
        {
            _segs          = segs;
            DoubleBuffered  = true;
            BackColor       = Color.FromArgb(20, 20, 20);

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var s in segs)
            {
                Expand(ref minX, ref maxX, (float)s.From.X, (float)s.To.X);
                Expand(ref minY, ref maxY, (float)s.From.Y, (float)s.To.Y);
                Expand(ref minZ, ref maxZ, (float)s.From.Z, (float)s.To.Z);
            }

            _cx     = (minX + maxX) / 2f;
            _cy     = (minY + maxY) / 2f;
            _cz     = (minZ + maxZ) / 2f;
            _bbSize = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            if (_bbSize < 0.001f) _bbSize = 1f;
        }

        static void Expand(ref float lo, ref float hi, float a, float b)
        {
            if (a < lo) lo = a;  if (a > hi) hi = a;
            if (b < lo) lo = b;  if (b > hi) hi = b;
        }

        PointF Project(double px, double py, double pz)
        {
            double x = px - _cx, y = py - _cy, z = pz - _cz;

            double yr = _yaw   * Math.PI / 180.0;
            double x1 = x * Math.Cos(yr) - y * Math.Sin(yr);
            double y1 = x * Math.Sin(yr) + y * Math.Cos(yr);

            double pr = _pitch * Math.PI / 180.0;
            double x2 = x1;
            double y2 = y1 * Math.Cos(pr) - z * Math.Sin(pr);

            float sc = _zoom * Math.Min(Width, Height) * 0.75f / _bbSize;
            return new PointF(
                Width  / 2f + _panX + (float)(x2 * sc),
                Height / 2f + _panY - (float)(y2 * sc));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Width, h = Height;

            foreach (var s in _segs)
            {
                var p1 = Project(s.From.X, s.From.Y, s.From.Z);
                var p2 = Project(s.To.X,   s.To.Y,   s.To.Z);

                if (p1.X < -50 && p2.X < -50) continue;
                if (p1.X > w+50 && p2.X > w+50) continue;
                if (p1.Y < -50 && p2.Y < -50) continue;
                if (p1.Y > h+50 && p2.Y > h+50) continue;

                var pen = s.MoveType == MoveType.Rapid ? _rapidPen
                        : s.MoveType == MoveType.Arc   ? _arcPen
                        : _cutPen;
                try { g.DrawLine(pen, p1, p2); } catch { }
            }

            // Legend
            int lx = 10, ly = Height - 66;
            Swatch(g, lx, ly,      Color.FromArgb(70,  130, 220), "Rapid (G0)");
            Swatch(g, lx, ly + 22, Color.FromArgb(220,  90,  40), "Cut (G1)");
            Swatch(g, lx, ly + 44, Color.FromArgb( 60, 180,  80), "Arc (G2/G3)");

            // Hint
            g.DrawString("Drag: orbit   Right-drag: pan   Scroll: zoom   Dbl-click: reset",
                _uiFont, Brushes.DimGray, 10, 10);
            g.DrawString(_segs.Count.ToString("N0") + " segments",
                _uiFont, Brushes.DimGray, 10, 26);
        }

        void Swatch(Graphics g, int x, int y, Color c, string txt)
        {
            using (var b = new SolidBrush(c))
                g.FillRectangle(b, x, y + 6, 14, 3);
            g.DrawString(txt, _uiFont, Brushes.Silver, x + 20, y);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _lastMouse = e.Location;
            _leftDown  = e.Button == MouseButtons.Left;
            _rightDown = e.Button == MouseButtons.Right;
            Capture    = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_leftDown && !_rightDown) return;
            int dx = e.X - _lastMouse.X;
            int dy = e.Y - _lastMouse.Y;
            _lastMouse = e.Location;
            if (_leftDown)
            {
                _yaw   += dx * 0.5f;
                _pitch  = Clamp(_pitch + dy * 0.5f, -89f, 89f);
            }
            else { _panX += dx; _panY += dy; }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _leftDown = _rightDown = false;
            Capture   = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _zoom = Clamp(_zoom * (e.Delta > 0 ? 1.15f : 0.87f), 0.01f, 500f);
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            _yaw = -45f; _pitch = 30f; _zoom = 1f; _panX = 0f; _panY = 0f;
            Invalidate();
        }

        static float Clamp(float v, float lo, float hi) =>
            v < lo ? lo : v > hi ? hi : v;

        protected override void Dispose(bool d)
        {
            if (d) { _rapidPen.Dispose(); _cutPen.Dispose();
                     _arcPen.Dispose();   _uiFont.Dispose(); }
            base.Dispose(d);
        }
    }
}
