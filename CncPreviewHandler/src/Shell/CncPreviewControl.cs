using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CncPreviewHandler.Diagnostics;
using CncPreviewHandler.Parser;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    public class CncPreviewControl : PreviewHandlerControl
    {
        private Label _lbl;

        public CncPreviewControl(string filePath)
        {
            BackColor = Color.FromArgb(20, 20, 20);
            _lbl = new Label
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
            Controls.Add(_lbl);
            if (string.IsNullOrEmpty(filePath))
            {
                Diag.Warn("CncPreviewControl ctor with empty path");
                return;
            }
            var _ = Handle;
            Task.Run(() => ParsePipeline(filePath));
        }

        private void ParsePipeline(string filePath)
        {
            List<ToolpathSegment> segs = null;
            string error = null, dialect = null;
            var t0 = Environment.TickCount;
            try
            {
                Diag.Info($"Parse pipeline start: {filePath}");
                try
                {
                    var attr = File.GetAttributes(filePath);
                    bool isCloud = (attr & FileAttributes.ReparsePoint) != 0 ||
                                   (attr & FileAttributes.Offline) != 0;
                    Diag.Info($"  attributes={attr} cloud-only={isCloud}");
                    if (isCloud)
                    {
                        SafeInvoke(() =>
                        {
                            _lbl.ForeColor = Color.FromArgb(255, 200, 0);
                            _lbl.Text =
                                "File is OneDrive cloud-only and not accessible from the preview pane.\r\n\r\n" +
                                "Right-click the file in Explorer and select\r\n" +
                                "\u201cAlways keep on this device\u201d, then try again.";
                        });
                        return;
                    }
                }
                catch (Exception ex) { Diag.Warn("attribute check failed: " + ex.Message); }

                try { dialect = DialectDetector.Detect(filePath); } catch { }
                Diag.Info($"  dialect={dialect ?? "(unknown)"}");

                SafeInvoke(() => _lbl.Text = "Parsing toolpath\u2026");
                segs = new GCodeParser().Parse(filePath);
                Diag.Info($"  parsed {segs?.Count ?? 0} segments in {Environment.TickCount - t0} ms");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Diag.Error("Parse pipeline threw", ex);
            }

            var capSegs = segs; var capDial = dialect; var capErr = error;
            SafeInvoke(() =>
            {
                try
                {
                    if (capErr != null)
                    { _lbl.ForeColor = Color.OrangeRed; _lbl.Text = "Error: " + capErr; return; }
                    if (capSegs == null || capSegs.Count == 0)
                    { _lbl.ForeColor = Color.OrangeRed; _lbl.Text = "No toolpath moves found"; return; }
                    Controls.Clear();
                    var vp = new ToolpathViewport(capSegs, filePath, capDial)
                        { Dock = DockStyle.Fill };
                    Controls.Add(vp);
                    vp.Focus();
                    Diag.Info("Viewport mounted");
                }
                catch (Exception ex) { Diag.Error("Viewport mount failed", ex); }
            });
        }

        private void SafeInvoke(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
            catch (Exception ex) { Diag.Warn("SafeInvoke failed: " + ex.Message); }
        }
    }

    sealed class ToolpathViewport : Control
    {
        // Data
        private readonly List<ToolpathSegment> _segs;
        private readonly string _fileName, _dialect;
        private readonly double _rangeX, _rangeY, _rangeZ;
        private readonly double _totalTravelMm, _cutTravelMm;
        private readonly double _estTimeMin;
        private readonly int _layerCount;

        // Camera
        private float _cx, _cy, _cz, _bbSize;
        private float _yaw=-45f, _pitch=30f, _zoom=1f, _panX, _panY;

        // Mouse
        private Point _lastMouse, _cursorPos;
        private bool  _cursorVisible, _leftDown, _rightDown;

        // Layer slider (only created when file is layered)
        private TrackBar _layerSlider;
        private int _layerSliderHeight;

        // Progressive render budget
        private int _renderBudget;
        private System.Windows.Forms.Timer _progressiveTimer;
        private const int InitialBudget = 5000;

        // Resources
        private readonly Pen  _rapidPen = new Pen(Color.FromArgb(70,130,220), 1f);
        private readonly Pen  _cutPen   = new Pen(Color.FromArgb(220,90,40),  1f);
        private readonly Pen  _arcPen   = new Pen(Color.FromArgb(60,180,80),  1f);
        private readonly Font _uiFont   = new Font("Segoe UI", 8.25f);
        private readonly Font _titleFont= new Font("Segoe UI", 9f, FontStyle.Bold);
        private readonly StringFormat _rightAlign =
            new StringFormat { Alignment = StringAlignment.Far };

        public ToolpathViewport(List<ToolpathSegment> segs, string filePath, string dialect)
        {
            _segs     = segs;
            _fileName = string.IsNullOrEmpty(filePath) ? "" : Path.GetFileName(filePath);
            _dialect  = dialect;

            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 20, 20);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            // Bounding box, travel stats, time estimate, layer count in one pass
            float x0=float.MaxValue,x1=float.MinValue;
            float y0=float.MaxValue,y1=float.MinValue;
            float z0=float.MaxValue,z1=float.MinValue;
            double tot=0, cut=0, mins=0;
            int maxLayer = 0;
            foreach (var s in segs)
            {
                Exp(ref x0,ref x1,(float)s.From.X,(float)s.To.X);
                Exp(ref y0,ref y1,(float)s.From.Y,(float)s.To.Y);
                Exp(ref z0,ref z1,(float)s.From.Z,(float)s.To.Z);
                double d = (s.To - s.From).Length;
                tot += d;
                if (s.MoveType != MoveType.Rapid) cut += d;
                double f = s.FeedrateMmPerMin;
                if (f <= 0) f = (s.MoveType == MoveType.Rapid) ? 6000 : 1500;
                mins += d / f;
                if (s.LayerIndex > maxLayer) maxLayer = s.LayerIndex;
            }
            _cx=(x0+x1)/2f; _cy=(y0+y1)/2f; _cz=(z0+z1)/2f;
            _rangeX=x1-x0; _rangeY=y1-y0; _rangeZ=z1-z0;
            _bbSize=Math.Max((float)_rangeX,Math.Max((float)_rangeY,(float)_rangeZ));
            if (_bbSize<0.001f) _bbSize=1f;
            _totalTravelMm = tot;
            _cutTravelMm   = cut;
            _estTimeMin    = mins;
            _layerCount    = maxLayer + 1;

            // Layer slider only for genuinely layered files
            if (_layerCount > 3 && _layerCount < 2000)
            {
                _layerSlider = new TrackBar
                {
                    Dock          = DockStyle.Bottom,
                    Minimum       = 0,
                    Maximum       = _layerCount - 1,
                    Value         = _layerCount - 1,
                    Height        = 32,
                    BackColor     = Color.FromArgb(35, 35, 35),
                    TickStyle     = TickStyle.None,
                    AutoSize      = false,
                    LargeChange   = Math.Max(1, _layerCount / 20),
                    SmallChange   = 1
                };
                _layerSlider.ValueChanged += (s, e) => Invalidate();
                _layerSliderHeight = _layerSlider.Height;
                Controls.Add(_layerSlider);
            }

            // Progressive rendering for big files
            if (_segs.Count > InitialBudget)
            {
                _renderBudget = InitialBudget;
                _progressiveTimer = new System.Windows.Forms.Timer { Interval = 60 };
                _progressiveTimer.Tick += OnProgressiveTick;
                _progressiveTimer.Start();
            }
            else _renderBudget = _segs.Count;
        }

        private void OnProgressiveTick(object sender, EventArgs e)
        {
            _renderBudget = Math.Min(_segs.Count, _renderBudget * 2);
            if (_renderBudget >= _segs.Count)
            {
                _progressiveTimer.Stop();
                _progressiveTimer.Dispose();
                _progressiveTimer = null;
            }
            Invalidate();
        }

        static void Exp(ref float lo,ref float hi,float a,float b)
        { if(a<lo)lo=a; if(a>hi)hi=a; if(b<lo)lo=b; if(b>hi)hi=b; }

        // Effective drawing height excludes the layer slider strip
        int DrawHeight => Math.Max(1, Height - _layerSliderHeight);

        PointF Proj(double px,double py,double pz)
        {
            double x=px-_cx,y=py-_cy,z=pz-_cz;
            double yr=_yaw*Math.PI/180.0;
            double cy=Math.Cos(yr), sy=Math.Sin(yr);
            double x1=x*cy-y*sy, y1=x*sy+y*cy;
            double pr=_pitch*Math.PI/180.0;
            double cp=Math.Cos(pr), sp=Math.Sin(pr);
            double x2=x1, y2=y1*cp-z*sp;
            int    h = DrawHeight;
            float  sc=_zoom*Math.Min(Width,h)*0.75f/_bbSize;
            return new PointF(Width/2f+_panX+(float)(x2*sc),
                              h/2f+_panY-(float)(y2*sc));
        }

        bool TryUnproj(PointF screen, out double wx, out double wy, out double wz)
        {
            wx=wy=wz=0;
            try
            {
                int    h  = DrawHeight;
                float  sc = _zoom*Math.Min(Width,h)*0.75f/_bbSize;
                if (sc < 1e-6f) return false;
                double x2 = (screen.X - Width/2.0  - _panX) / sc;
                double y2 = -(screen.Y - h/2.0     - _panY) / sc;
                double pr = _pitch*Math.PI/180.0;
                double cp = Math.Cos(pr), sp = Math.Sin(pr);
                if (Math.Abs(cp) < 1e-6) return false;
                double y1 = (y2 + 0.0*sp) / cp;
                double x1 = x2;
                double yr = _yaw*Math.PI/180.0;
                double cy = Math.Cos(yr), sy = Math.Sin(yr);
                wx = x1*cy + y1*sy + _cx;
                wy = -x1*sy + y1*cy + _cy;
                wz = _cz;
                return true;
            }
            catch { return false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                float w = Width, h = DrawHeight;

                int currentLayer = _layerSlider != null ? _layerSlider.Value : int.MaxValue;
                int budget = (_leftDown || _rightDown) ? _segs.Count : _renderBudget;

                int drawn = 0;
                foreach (var s in _segs)
                {
                    if (drawn >= budget) break;
                    if (s.LayerIndex > currentLayer) continue;

                    var p1 = Proj(s.From.X, s.From.Y, s.From.Z);
                    var p2 = Proj(s.To.X,   s.To.Y,   s.To.Z);
                    if (p1.X<-50 && p2.X<-50) { drawn++; continue; }
                    if (p1.X>w+50 && p2.X>w+50) { drawn++; continue; }
                    if (p1.Y<-50 && p2.Y<-50) { drawn++; continue; }
                    if (p1.Y>h+50 && p2.Y>h+50) { drawn++; continue; }
                    var pen = s.MoveType == MoveType.Rapid ? _rapidPen :
                              s.MoveType == MoveType.Arc   ? _arcPen   : _cutPen;
                    g.DrawLine(pen, p1, p2);
                    drawn++;
                }

                DrawInfoPanel(g, currentLayer);
                DrawLegend(g);
                DrawHints(g);
                DrawCursorReadout(g);
                DrawProgressiveBadge(g, budget);
            }
            catch (Exception ex) { Diag.Error("OnPaint failed", ex); }
        }

        void DrawInfoPanel(Graphics g, int currentLayer)
        {
            int x = 10, y = 10;
            using (var primary   = new SolidBrush(Color.FromArgb(235, 235, 235)))
            using (var secondary = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                if (!string.IsNullOrEmpty(_fileName))
                { g.DrawString(_fileName, _titleFont, primary, x, y); y += 18; }
                if (!string.IsNullOrEmpty(_dialect))
                { g.DrawString(_dialect, _uiFont, secondary, x, y); y += 14; }
                g.DrawString(
                    $"Size: {_rangeX:F1} \u00D7 {_rangeY:F1} \u00D7 {_rangeZ:F1} mm",
                    _uiFont, secondary, x, y); y += 14;
                g.DrawString(
                    $"{_segs.Count:N0} moves \u00B7 {_totalTravelMm/1000:F2} m total \u00B7 {_cutTravelMm/1000:F2} m cut",
                    _uiFont, secondary, x, y); y += 14;
                g.DrawString(
                    "Est time: " + FormatTime(_estTimeMin),
                    _uiFont, secondary, x, y); y += 14;

                if (_layerSlider != null && currentLayer < _layerCount - 1)
                {
                    using (var hi = new SolidBrush(Color.FromArgb(255, 200, 0)))
                        g.DrawString(
                            $"Showing layer {currentLayer + 1} / {_layerCount}",
                            _titleFont, hi, x, y);
                }
            }
        }

        static string FormatTime(double m)
        {
            if (m < 1)  return $"{(int)(m*60)} s";
            if (m < 60) return $"{(int)m} m {(int)((m-(int)m)*60):D2} s";
            int h = (int)(m / 60);
            int mm = (int)(m - h*60);
            return $"{h} h {mm:D2} m";
        }

        void DrawLegend(Graphics g)
        {
            int x = 10, y = DrawHeight - 66;
            Sw(g, x, y,      Color.FromArgb(70, 130, 220), "Rapid (G0)");
            Sw(g, x, y + 22, Color.FromArgb(220, 90, 40),  "Cut (G1)");
            Sw(g, x, y + 44, Color.FromArgb(60, 180, 80),  "Arc (G2/G3)");
        }

        void Sw(Graphics g, int x, int y, Color c, string t)
        {
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y + 6, 14, 3);
            g.DrawString(t, _uiFont, Brushes.Silver, x + 20, y);
        }

        void DrawHints(Graphics g)
        {
            string l1 = "drag=orbit  R-drag=pan  scroll=zoom  2\u00D7click=reset";
            string l2 = "[1] top  [2] front  [3] right  [4] left  [5] iso  [6] reset";
            g.DrawString(l1, _uiFont, Brushes.DimGray,
                new RectangleF(0, 10, Width-10, 14), _rightAlign);
            g.DrawString(l2, _uiFont, Brushes.DimGray,
                new RectangleF(0, 26, Width-10, 14), _rightAlign);
        }

        void DrawCursorReadout(Graphics g)
        {
            if (!_cursorVisible) return;
            if (_cursorPos.Y >= DrawHeight) return;   // mouse over slider
            if (!TryUnproj(_cursorPos, out double wx, out double wy, out double wz)) return;
            string txt = $"X {wx,8:F2}   Y {wy,8:F2}   Z {wz,8:F2}";
            var sz = g.MeasureString(txt, _uiFont);
            using (var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                g.FillRectangle(bg,
                    Width - sz.Width - 14, DrawHeight - sz.Height - 12,
                    sz.Width + 8, sz.Height + 4);
            g.DrawString(txt, _uiFont, Brushes.Silver,
                Width - sz.Width - 10, DrawHeight - sz.Height - 10);
        }

        void DrawProgressiveBadge(Graphics g, int budget)
        {
            if (_progressiveTimer == null) return;
            string txt = $"Refining\u2026 {budget * 100 / Math.Max(1,_segs.Count)}%";
            var sz = g.MeasureString(txt, _uiFont);
            g.DrawString(txt, _uiFont, Brushes.DarkGray,
                Width - sz.Width - 10, DrawHeight - sz.Height - 32);
        }

        protected override void OnMouseEnter(EventArgs e)
        { Focus(); _cursorVisible = true; base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e)
        { _cursorVisible = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        { _lastMouse=e.Location; _leftDown=e.Button==MouseButtons.Left;
          _rightDown=e.Button==MouseButtons.Right; Capture=true; Focus(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            _cursorPos = e.Location;
            _cursorVisible = true;
            if (_leftDown || _rightDown)
            {
                int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
                _lastMouse = e.Location;
                if (_leftDown) { _yaw += dx*0.5f; _pitch = Cl(_pitch + dy*0.5f, -89f, 89f); }
                else           { _panX += dx; _panY += dy; }
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        { _leftDown = _rightDown = false; Capture = false; }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        { _yaw=-45f; _pitch=30f; _zoom=1f; _panX=0f; _panY=0f; Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                _zoom = Cl(_zoom * (delta > 0 ? 1.15f : 0.87f), 0.01f, 500f);
                Invalidate();
                return;
            }
            base.WndProc(ref m);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.D1: case Keys.D2: case Keys.D3:
                case Keys.D4: case Keys.D5: case Keys.D6:
                case Keys.NumPad1: case Keys.NumPad2: case Keys.NumPad3:
                case Keys.NumPad4: case Keys.NumPad5: case Keys.NumPad6:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.D1: case Keys.NumPad1: SetView(  0,   0); break;
                case Keys.D2: case Keys.NumPad2: SetView(  0, -89); break;
                case Keys.D3: case Keys.NumPad3: SetView(-90, -89); break;
                case Keys.D4: case Keys.NumPad4: SetView( 90, -89); break;
                case Keys.D5: case Keys.NumPad5: SetView(-45,  30); break;
                case Keys.D6: case Keys.NumPad6:
                    _yaw=-45f; _pitch=30f; _zoom=1f; _panX=0f; _panY=0f;
                    Invalidate(); break;
                default: return;
            }
            e.Handled = true;
        }

        void SetView(float yaw, float pitch)
        { _yaw = yaw; _pitch = pitch; Invalidate(); }

        static float Cl(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        protected override void Dispose(bool d)
        {
            if (d)
            {
                _rapidPen.Dispose(); _cutPen.Dispose();
                _arcPen.Dispose();   _uiFont.Dispose();
                _titleFont.Dispose(); _rightAlign.Dispose();
                if (_progressiveTimer != null) { _progressiveTimer.Dispose(); _progressiveTimer = null; }
            }
            base.Dispose(d);
        }
    }
}
