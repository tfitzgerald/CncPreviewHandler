using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using CncPreviewHandler.Parser;
using CncPreviewHandler.Renderer;
using HelixToolkit.Wpf;
using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;

namespace CncPreviewHandler.Shell
{
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
            try
            {
                if (string.IsNullOrEmpty(_filePath)) return DrawFallback((int)squareSize);
                var segments = new GCodeParser().Parse(_filePath);
                return RenderOffscreen(segments, (int)squareSize);
            }
            catch { return DrawFallback((int)squareSize); }
        }

        private static Bitmap RenderOffscreen(
            System.Collections.Generic.IList<ToolpathSegment> segments, int size)
        {
            Bitmap result = null;
            var thread = new Thread(() =>
            {
                var viewport = new Viewport3D { Width = size, Height = size };
                var bounds = ToolpathGeometryBuilder.ComputeBounds(segments);
                double diag = Math.Sqrt(
                    bounds.RangeX * bounds.RangeX + bounds.RangeY * bounds.RangeY) + 1;
                viewport.Camera = new PerspectiveCamera
                {
                    Position      = new Point3D(bounds.Center.X + diag, bounds.Center.Y - diag, bounds.Center.Z + diag * 0.8),
                    LookDirection = new Vector3D(-1, 1, -0.8),
                    UpDirection   = new Vector3D(0, 0, 1),
                    FieldOfView   = 40
                };
                var lights = new ModelVisual3D();
                lights.Content = new Model3DGroup
                {
                    Children =
                    {
                        new AmbientLight(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                        new DirectionalLight(System.Windows.Media.Colors.White, new Vector3D(-1, -1, -1))
                    }
                };
                viewport.Children.Add(lights);
                ToolpathGeometryBuilder.Build(segments, out var r1, out var c1, out var a1);
                viewport.Children.Add(r1);
                viewport.Children.Add(c1);
                viewport.Children.Add(a1);
                viewport.Measure(new System.Windows.Size(size, size));
                viewport.Arrange(new Rect(0, 0, size, size));
                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(viewport);
                using (var ms = new MemoryStream())
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    enc.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    result = new Bitmap(ms);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(10));
            return result ?? DrawFallback(size);
        }

        private static Bitmap DrawFallback(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 2))
            using (var font = new Font("Segoe UI", Math.Max(8, size / 10f)))
            using (var brush = new SolidBrush(System.Drawing.Color.OrangeRed))
            {
                g.Clear(System.Drawing.Color.FromArgb(30, 30, 30));
                g.DrawRectangle(pen, 4, 4, size - 8, size - 8);
                var text = "CNC";
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (size - sz.Width) / 2, (size - sz.Height) / 2);
            }
            return bmp;
        }
    }
}
