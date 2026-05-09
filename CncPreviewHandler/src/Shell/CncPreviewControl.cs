using System;
using System.Windows;
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
    public class CncPreviewControl : PreviewHandlerControl
    {
        public CncPreviewControl(string filePath)
        {
            BackColor = System.Drawing.Color.Black;

            if (string.IsNullOrEmpty(filePath))
            {
                Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "No file path received",
                    ForeColor = System.Drawing.Color.OrangeRed,
                    BackColor = System.Drawing.Color.Black,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Font = new System.Drawing.Font("Segoe UI", 11f)
                });
                return;
            }

            // WPF requires an Application instance for mouse/keyboard event routing
            if (Application.Current == null)
            {
                try
                {
                    new Application
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }
                catch { }
            }

            var loadLabel = new System.Windows.Controls.Label
            {
                Content    = "Parsing toolpath\u2026",
                Foreground = Brushes.Gray,
                FontSize   = 13,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment   = VerticalAlignment.Center,
            };

            var host = new ElementHost
            {
                Dock  = DockStyle.Fill,
                Child = loadLabel
            };
            Controls.Add(host);

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var segments = new GCodeParser().Parse(filePath);

                    if (segments.Count == 0)
                    {
                        Invoke((Action)(() =>
                            loadLabel.Content = "No toolpath moves found in file."));
                        return;
                    }

                    // Build viewport on UI thread (WPF requirement)
                    Invoke((Action)(() =>
                    {
                        try
                        {
                            host.Child = BuildViewport(segments);
                        }
                        catch (Exception ex)
                        {
                            loadLabel.Content    = "Render error: " + ex.Message;
                            loadLabel.Foreground = Brushes.OrangeRed;
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Invoke((Action)(() =>
                    {
                        loadLabel.Content    = "Parse error: " + ex.Message;
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
                IsHeadLightEnabled    = true,
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
                    MinorDistance = Math.Max(1, bounds.RangeX / 20),
                    MajorDistance = Math.Max(5, bounds.RangeX / 4),
                    Thickness     = 0.3,
                });
            }

            double dist = Math.Sqrt(
                bounds.RangeX * bounds.RangeX +
                bounds.RangeY * bounds.RangeY) + 50;

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
