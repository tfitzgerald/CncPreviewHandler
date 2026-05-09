using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CncPreviewHandler.Parser;
using CncPreviewHandler.Renderer;
using HelixToolkit.Wpf;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    /// <summary>
    /// The actual preview UI control.
    /// Extends PreviewHandlerControl (a SharpShell WinForms UserControl).
    /// Hosts a HelixToolkit WPF 3D viewport via ElementHost.
    /// Parsing runs on a background thread so Explorer stays responsive.
    /// </summary>
    public class CncPreviewControl : PreviewHandlerControl
    {
        public CncPreviewControl(string filePath)
        {
            // Loading placeholder shown immediately
            var loadLabel = new System.Windows.Controls.Label
            {
                Content    = "Parsing toolpath…",
                Foreground = Brushes.Gray,
                FontSize   = 13,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalContentAlignment   = System.Windows.VerticalAlignment.Center,
            };

            var host = new ElementHost { Dock = DockStyle.Fill, Child = loadLabel };
            Controls.Add(host);

            // Parse on a background thread — large files can be slow
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var segments = new GCodeParser().Parse(filePath);
                    var viewport = BuildViewport(segments);
                    Invoke((Action)(() => host.Child = viewport));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        loadLabel.Content    = $"Preview failed: {ex.Message}";
                        loadLabel.Foreground = Brushes.OrangeRed;
                    }));
                }
            });
        }

        private static HelixViewport3D BuildViewport(
            System.Collections.Generic.IList<ToolpathSegment> segments)
        {
            var viewport = new HelixViewport3D
            {
                Background            = Brushes.Black,
                ShowCoordinateSystem  = true,
                ZoomExtentsWhenLoaded = true,
            };

            viewport.Children.Add(new DefaultLights());

            ToolpathGeometryBuilder.Build(segments,
                out var rapidLines, out var cutLines, out var arcLines);

            viewport.Children.Add(rapidLines);
            viewport.Children.Add(cutLines);
            viewport.Children.Add(arcLines);

            var bounds = ToolpathGeometryBuilder.ComputeBounds(segments);
            if (bounds.RangeX > 0 && bounds.RangeY > 0)
            {
                viewport.Children.Add(new GridLinesVisual3D
                {
                    Center        = new Point3D(bounds.Center.X, bounds.Center.Y, bounds.MinZ),
                    Width         = bounds.RangeX * 1.2,
                    Length        = bounds.RangeY * 1.2,
                    MinorDistance = bounds.RangeX / 20,
                    MajorDistance = bounds.RangeX / 4,
                    Thickness     = 0.3,
                });
            }

            double dist = Math.Sqrt(
                bounds.RangeX * bounds.RangeX + bounds.RangeY * bounds.RangeY) + 50;

            viewport.Camera = new PerspectiveCamera
            {
                Position      = new Point3D(
                    bounds.Center.X + dist,
                    bounds.Center.Y - dist * 1.2,
                    bounds.Center.Z + dist),
                LookDirection = new Vector3D(-1, 1.2, -1),
                UpDirection   = new Vector3D(0, 0, 1),
                FieldOfView   = 45
            };

            return viewport;
        }
    }
}
