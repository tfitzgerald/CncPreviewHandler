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

            // Force handle creation now so BeginInvoke works
            var _ = Handle;

            Task.Run(() => ParsePipeline(filePath));
        }

        private void ParsePipeline(string filePath)
        {
            List<ToolpathSegment> segs = null;
            string error = null;
            var t0 = Environment.TickCount;

            try
            {
                Diag.Info($"Parse pipeline start: {filePath}");

                // Cloud-only file detection
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
                        Diag.Warn("Aborting parse: file is cloud-only");
                        return;
                    }
                }
                catch (Exception ex) { Diag.Warn("attribute check failed: " + ex.Message); }

                SafeInvoke(() => _lbl.Text = "Parsing toolpath\u2026");

                var parser = new GCodeParser();
                segs = parser.Parse(filePath);

                Diag.Info($"  parsed {segs?.Count ?? 0} segments in {Environment.TickCount - t0} ms");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Diag.Error("Parse pipeline threw", ex);
            }

            SafeInvoke(() =>
            {
                try
                {
                    if (error != null)
                    {
                        _lbl.ForeColor = Color.OrangeRed;
                        _lbl.Text      = "Error: " + error;
                        return;
                    }
                    if (segs == null || segs.Count == 0)
                    {
                        _lbl.ForeColor = Color.OrangeRed;
                        _lbl.Text      = "No toolpath moves found";
                        return;
                    }
                    Controls.Clear();
                    var vp = new ToolpathViewport(segs) { Dock = DockStyle.Fill };
                    Controls.Add(vp);
                    vp.Focus();
                    Diag.Info("Viewport mounted successfully");
                }
                catch (Exception ex)
                {
                    Diag.Error("Viewport mount failed", ex);
                    try
                    {
                        Controls.Clear();
                        _lbl = new Label
                        {
                            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                            ForeColor = Color.OrangeRed, BackColor = Color.FromArgb(20,20,20),
                            Font = new Font("Segoe UI", 11f),
                            Text = "Render error: " + ex.Message
                        };
                        Controls.Add(_lbl);
                    }
                    catch { }
                }
            });
        }

        private void SafeInvoke(Action a)
        {
            try
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke(a);
            }
            catch (Exception ex) { Diag.Warn("SafeInvoke failed: " + ex.Message); }
        }
    }

    sealed class ToolpathViewport : Control
    {
        private readonly List<ToolpathSegment> _segs;
        private float _cx, _cy, _cz, _bbSize;
        private float _yaw=-45f, _pitch=30f, _zoom=1f, _panX, _panY;
        private Point _lastMouse;
        private bool  _leftDown, _rightDown;
        private readonly Pen  _rapidPen = new Pen(Color.FromArgb(70,130,220), 1f);
        private readonly Pen  _cutPen   = new Pen(Color.FromArgb(220,90,40),  1f);
        private readonly Pen  _arcPen   = new Pen(Color.FromArgb(60,180,80),  1f);
        private readonly Font _uiFont   = new Font("Segoe UI", 8f);

        public ToolpathViewport(List<ToolpathSegment> segs)
        {
            _segs = segs;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 20, 20);
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;

            float x0=float.MaxValue,x1=float.MinValue;
            float y0=float.MaxValue,y1=float.MinValue;
            float z0=float.MaxValue,z1=float.MinValue;
            foreach (var s in segs)
            {
                Exp(ref x0,ref x1,(float)s.From.X,(float)s.To.X);
                Exp(ref y0,ref y1,(float)s.From.Y,(float)s.To.Y);
                Exp(ref z0,ref z1,(float)s.From.Z,(float)s.To.Z);
            }
            _cx=(x0+x1)/2f; _cy=(y0+y1)/2f; _cz=(z0+z1)/2f;
            _bbSize=Math.Max(x1-x0,Math.Max(y1-y0,z1-z0));
            if(_bbSize<0.001f)_bbSize=1f;
        }

        protected override void OnMouseEnter(EventArgs e) { Focus(); base.OnMouseEnter(e); }

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

        static void Exp(ref float lo,ref float hi,float a,float b)
        { if(a<lo)lo=a; if(a>hi)hi=a; if(b<lo)lo=b; if(b>hi)hi=b; }

        PointF Proj(double px,double py,double pz)
        {
            double x=px-_cx,y=py-_cy,z=pz-_cz;
            double yr=_yaw*Math.PI/180.0;
            double x1=x*Math.Cos(yr)-y*Math.Sin(yr);
            double y1=x*Math.Sin(yr)+y*Math.Cos(yr);
            double pr=_pitch*Math.PI/180.0;
            double x2=x1,y2=y1*Math.Cos(pr)-z*Math.Sin(pr);
            float sc=_zoom*Math.Min(Width,Height)*0.75f/_bbSize;
            return new PointF(Width/2f+_panX+(float)(x2*sc),
                              Height/2f+_panY-(float)(y2*sc));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g=e.Graphics;
                g.SmoothingMode=SmoothingMode.AntiAlias;
                float w=Width,h=Height;
                foreach(var s in _segs)
                {
                    var p1=Proj(s.From.X,s.From.Y,s.From.Z);
                    var p2=Proj(s.To.X,s.To.Y,s.To.Z);
                    if(p1.X<-50&&p2.X<-50)continue;
                    if(p1.X>w+50&&p2.X>w+50)continue;
                    if(p1.Y<-50&&p2.Y<-50)continue;
                    if(p1.Y>h+50&&p2.Y>h+50)continue;
                    var pen=s.MoveType==MoveType.Rapid?_rapidPen:
                            s.MoveType==MoveType.Arc?_arcPen:_cutPen;
                    g.DrawLine(pen,p1,p2);
                }
                int lx=10,ly=Height-66;
                Sw(g,lx,ly,    Color.FromArgb(70,130,220),"Rapid (G0)");
                Sw(g,lx,ly+22, Color.FromArgb(220,90,40), "Cut (G1)");
                Sw(g,lx,ly+44, Color.FromArgb(60,180,80), "Arc (G2/G3)");
                g.DrawString("Drag: orbit   Right-drag: pan   Scroll: zoom   Dbl-click: reset",
                    _uiFont,Brushes.DimGray,10,10);
                g.DrawString(_segs.Count.ToString("N0")+" segments",
                    _uiFont,Brushes.DimGray,10,26);
            }
            catch (Exception ex) { Diag.Error("OnPaint failed", ex); }
        }

        void Sw(Graphics g,int x,int y,Color c,string t)
        {
            using(var b=new SolidBrush(c))g.FillRectangle(b,x,y+6,14,3);
            g.DrawString(t,_uiFont,Brushes.Silver,x+20,y);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        { _lastMouse=e.Location; _leftDown=e.Button==MouseButtons.Left;
          _rightDown=e.Button==MouseButtons.Right; Capture=true; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if(!_leftDown&&!_rightDown)return;
            int dx=e.X-_lastMouse.X,dy=e.Y-_lastMouse.Y;
            _lastMouse=e.Location;
            if(_leftDown){_yaw+=dx*0.5f;_pitch=Cl(_pitch+dy*0.5f,-89f,89f);}
            else{_panX+=dx;_panY+=dy;}
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        { _leftDown=_rightDown=false; Capture=false; }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        { _yaw=-45f;_pitch=30f;_zoom=1f;_panX=0f;_panY=0f; Invalidate(); }

        static float Cl(float v,float lo,float hi)=>v<lo?lo:v>hi?hi:v;

        protected override void Dispose(bool d)
        { if(d){_rapidPen.Dispose();_cutPen.Dispose();
                _arcPen.Dispose();_uiFont.Dispose();}
          base.Dispose(d); }
    }
}
