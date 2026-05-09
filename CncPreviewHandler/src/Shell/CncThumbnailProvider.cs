using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
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
    /// <summary>
    /// Registers this assembly as the Windows Explorer Thumbnail Provider
    /// for .nc, .gcode, .tap, and .cnc files.
    /// Returns a fixed-angle isometric render as an HBITMAP.
    /// </summary>
    [ComVisible(true)]
    [Guid("C2D3E4F5-A6B7-8901-CDEF-012345678902")]
    [COMServerAssociation(AssociationType.ClassOfExtension,
        ".nc", ".gcode", ".gc", ".g", ".tap", ".cnc")]
    [DisplayName("CNC Toolpath Thumbnail Provider")]
    public class CncThumbnailProvider : SharpThumbnailHandler
    {
        protected override Bitmap GetThumbnailImage(uint squareSize)
        {
            try
            {
                var parser   = new GCodeParser();
                var segments = parser.Parse(SelectedItemPath);
                return RenderOffscreen(segments, (int)squareSize);
            }
            catch
            {
                // Return a plain placeholder rather than crashing Explorer
                return DrawFallback((int)squareSize);
            }
        }

        private static Bitmap RenderOffscreen(
            System.Collections.Generic.IList<ToolpathSegment> segments, int size)
        {
            Bitmap result = null;

            // WPF rendering must happen on an STA thread
            var thread = new System.Threading.Thread(() =>
            {
                var viewport = new Viewport3D
                {
                    Width  = size,
                    Height = size
                };

                // Camera — fixed isometric angle
                var bounds = ToolpathGeometryBuilder.ComputeBounds(segments);
                double diag = Math.Sqrt(
                    bounds.RangeX * bounds.RangeX + bounds.RangeY * bounds.RangeY) + 1;

                viewport.Camera = new PerspectiveCamera
                {
                    Position      = new Point3D(
                        bounds.Center.X + diag,
                        bounds.Center.Y - diag,
                        bounds.Center.Z + diag * 0.8),
                    LookDirection = new Vector3D(-1, 1, -0.8),
                    UpDirection   = new Vector3D(0, 0, 1),
                    FieldOfView   = 40
                };

                // Lighting
                var lights = new ModelVisual3D();
                lights.Content = new Model3DGroup
                {
                    Children =
                    {
                        new AmbientLight(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                        new DirectionalLight(System.Windows.Media.Colors.White,
                            new Vector3D(-1, -1, -1))
                    }
                };
                viewport.Children.Add(lights);

                ToolpathGeometryBuilder.Build(segments,
                    out var rapidLines, out var cutLines, out var arcLines);
                viewport.Children.Add(rapidLines);
                viewport.Children.Add(cutLines);
                viewport.Children.Add(arcLines);

                viewport.Measure(new System.Windows.Size(size, size));
                viewport.Arrange(new Rect(0, 0, size, size));

                var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(viewport);

                using var ms = new MemoryStream();
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                enc.Save(ms);
                ms.Seek(0, SeekOrigin.Begin);
                result = new Bitmap(ms);
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(10));

            return result ?? DrawFallback(size);
        }

        private static Bitmap DrawFallback(int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.FromArgb(30, 30, 30));
            using var pen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 2);
            g.DrawRectangle(pen, 4, 4, size - 8, size - 8);
            using var font  = new Font("Segoe UI", Math.Max(8, size / 10f));
            using var brush = new SolidBrush(System.Drawing.Color.OrangeRed);
            var text = "CNC";
            var sz   = g.MeasureString(text, font);
            g.DrawString(text, font, brush,
                (size - sz.Width) / 2, (size - sz.Height) / 2);
            return bmp;
        }
    }
}
