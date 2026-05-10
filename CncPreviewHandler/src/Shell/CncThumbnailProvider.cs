using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using CncPreviewHandler.Diagnostics;
using CncPreviewHandler.Parser;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace CncPreviewHandler.Shell
{
    /// <summary>
    /// Generates 3D toolpath thumbnails for CNC/gcode files in Explorer
    /// directory views. Renders an isometric projection to a square bitmap.
    /// </summary>
    [ComVisible(true)]
    [Guid("C2D3E4F5-A6B7-8901-CDEF-012345678902")]
    [COMServerAssociation(AssociationType.ClassOfExtension,
        ".nc", ".gcode", ".gc", ".g", ".tap", ".cnc")]
    [DisplayName("CNC Toolpath Thumbnail Provider")]
    public class CncThumbnailProvider : SharpThumbnailHandler, IInitializeWithFile
    {
        private string _filePath;

        void IInitializeWithFile.Initialize(string pszFilePath, uint grfMode)
        {
            _filePath = pszFilePath;
        }

        protected override Bitmap GetThumbnailImage(uint squareSize)
        {
            int sz = (int)squareSize;
            try
            {
                Diag.Info($"Thumbnail requested: {_filePath} size={sz}");
                if (string.IsNullOrEmpty(_filePath)) return Fallback(sz);

                try
                {
                    var attr = File.GetAttributes(_filePath);
                    if ((attr & FileAttributes.ReparsePoint) != 0 ||
                        (attr & FileAttributes.Offline) != 0)
                    {
                        Diag.Info("  cloud-only, returning fallback");
                        return Fallback(sz);
                    }
                }
                catch { }

                var segs = new GCodeParser().Parse(_filePath);
                if (segs == null || segs.Count == 0) return Fallback(sz);

                return Render(segs, sz);
            }
            catch (Exception ex)
            {
                Diag.Error("Thumbnail generation failed", ex);
                return Fallback(sz);
            }
        }

        Bitmap Render(List<ToolpathSegment> segs, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(20, 20, 20));

                float x0=float.MaxValue,x1=float.MinValue;
                float y0=float.MaxValue,y1=float.MinValue;
                float z0=float.MaxValue,z1=float.MinValue;
                foreach (var s in segs)
                {
                    x0 = Math.Min(x0, (float)Math.Min(s.From.X, s.To.X));
                    x1 = Math.Max(x1, (float)Math.Max(s.From.X, s.To.X));
                    y0 = Math.Min(y0, (float)Math.Min(s.From.Y, s.To.Y));
                    y1 = Math.Max(y1, (float)Math.Max(s.From.Y, s.To.Y));
                    z0 = Math.Min(z0, (float)Math.Min(s.From.Z, s.To.Z));
                    z1 = Math.Max(z1, (float)Math.Max(s.From.Z, s.To.Z));
                }
                float cx=(x0+x1)/2f, cy=(y0+y1)/2f, cz=(z0+z1)/2f;
                float bb = Math.Max(x1-x0, Math.Max(y1-y0, z1-z0));
                if (bb < 0.001f) bb = 1f;

                double yr = -45 * Math.PI / 180.0;
                double pr =  30 * Math.PI / 180.0;
                double cosY = Math.Cos(yr), sinY = Math.Sin(yr);
                double cosP = Math.Cos(pr), sinP = Math.Sin(pr);
                float sc = size * 0.7f / bb;

                using (var rapidPen = new Pen(Color.FromArgb(70,130,220), 1f))
                using (var cutPen   = new Pen(Color.FromArgb(220,90,40), 1.5f))
                using (var arcPen   = new Pen(Color.FromArgb(60,180,80), 1.5f))
                {
                    foreach (var s in segs)
                    {
                        var p1 = Proj(s.From.X-cx, s.From.Y-cy, s.From.Z-cz,
                                      sinY, cosY, sinP, cosP, sc, size);
                        var p2 = Proj(s.To.X-cx,   s.To.Y-cy,   s.To.Z-cz,
                                      sinY, cosY, sinP, cosP, sc, size);
                        var pen = s.MoveType == MoveType.Rapid ? rapidPen :
                                  s.MoveType == MoveType.Arc   ? arcPen   : cutPen;
                        try { g.DrawLine(pen, p1, p2); } catch { }
                    }
                }
            }
            return bmp;
        }

        static PointF Proj(double x, double y, double z,
                           double sinY, double cosY, double sinP, double cosP,
                           float sc, int size)
        {
            double x1 = x*cosY - y*sinY;
            double y1 = x*sinY + y*cosY;
            double y2 = y1*cosP - z*sinP;
            return new PointF(size/2f + (float)(x1*sc),
                              size/2f - (float)(y2*sc));
        }

        static Bitmap Fallback(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.FromArgb(220, 90, 40), 2))
            using (var font = new Font("Segoe UI", Math.Max(8, size / 8f), FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(220, 90, 40)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(30, 30, 30));
                int margin = Math.Max(2, size / 16);
                g.DrawRectangle(pen, margin, margin, size - 2*margin, size - 2*margin);
                var text = "CNC";
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, brush,
                    (size - sz.Width)/2f, (size - sz.Height)/2f);
            }
            return bmp;
        }
    }
}
