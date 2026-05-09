using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace CncPreviewHandler.Renderer
{
    /// <summary>
    /// Converts parsed ToolpathSegments into HelixToolkit visual models
    /// suitable for display in a Viewport3D.
    /// </summary>
    public static class ToolpathGeometryBuilder
    {
        // Conventional CNC toolpath colour scheme
        public static readonly Color ColorRapid = Color.FromRgb(70,  130, 220); // blue
        public static readonly Color ColorCut   = Color.FromRgb(220,  90,  40); // orange-red
        public static readonly Color ColorArc   = Color.FromRgb( 60, 180,  80); // green

        public static ToolpathBounds Build(
            IList<Parser.ToolpathSegment> segments,
            out LinesVisual3D rapidLines,
            out LinesVisual3D cutLines,
            out LinesVisual3D arcLines)
        {
            var rapidPts = new Point3DCollection();
            var cutPts   = new Point3DCollection();
            var arcPts   = new Point3DCollection();

            foreach (var seg in segments)
            {
                switch (seg.MoveType)
                {
                    case Parser.MoveType.Rapid:
                        rapidPts.Add(seg.From); rapidPts.Add(seg.To); break;
                    case Parser.MoveType.Cut:
                        cutPts.Add(seg.From);   cutPts.Add(seg.To);   break;
                    case Parser.MoveType.Arc:
                        arcPts.Add(seg.From);   arcPts.Add(seg.To);   break;
                }
            }

            rapidLines = new LinesVisual3D
            {
                Points    = rapidPts,
                Color     = ColorRapid,
                Thickness = 1.0
            };
            cutLines = new LinesVisual3D
            {
                Points    = cutPts,
                Color     = ColorCut,
                Thickness = 1.5
            };
            arcLines = new LinesVisual3D
            {
                Points    = arcPts,
                Color     = ColorArc,
                Thickness = 1.5
            };

            return ComputeBounds(segments);
        }

        public static ToolpathBounds ComputeBounds(IList<Parser.ToolpathSegment> segments)
        {
            if (!segments.Any())
                return new ToolpathBounds();

            var allPts = segments.SelectMany(s => new[] { s.From, s.To });
            return new ToolpathBounds
            {
                MinX = allPts.Min(p => p.X), MaxX = allPts.Max(p => p.X),
                MinY = allPts.Min(p => p.Y), MaxY = allPts.Max(p => p.Y),
                MinZ = allPts.Min(p => p.Z), MaxZ = allPts.Max(p => p.Z),
            };
        }
    }

    public class ToolpathBounds
    {
        public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
        public double RangeX => MaxX - MinX;
        public double RangeY => MaxY - MinY;
        public double RangeZ => MaxZ - MinZ;
        public Point3D Center => new Point3D(
            (MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);
    }
}
