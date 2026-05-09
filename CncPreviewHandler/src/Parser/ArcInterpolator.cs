using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    /// <summary>
    /// Converts G2/G3 arc commands into a polyline of short linear segments.
    /// Handles both I/J center-offset and R radius specifications.
    /// Respects G17 (XY), G18 (XZ), and G19 (YZ) arc planes.
    /// </summary>
    public static class ArcInterpolator
    {
        private const int StepsPerFullCircle = 72; // 5-degree resolution

        public static IEnumerable<ToolpathSegment> Expand(
            Point3D from, Point3D to,
            double? i, double? j, double? k, double? r,
            bool clockwise, ArcPlane plane)
        {
            Point3D center;
            double  radius;

            if (r.HasValue)
            {
                (center, radius) = ComputeCenterFromRadius(from, to, r.Value, clockwise, plane);
            }
            else
            {
                // I/J/K are offsets from start point to center
                double ci = i ?? 0, cj = j ?? 0, ck = k ?? 0;
                center = new Point3D(from.X + ci, from.Y + cj, from.Z + ck);
                radius = Math.Sqrt(ci * ci + cj * cj + ck * ck);
            }

            return TessellateArc(from, to, center, radius, clockwise, plane);
        }

        private static IEnumerable<ToolpathSegment> TessellateArc(
            Point3D from, Point3D to, Point3D center,
            double radius, bool clockwise, ArcPlane plane)
        {
            // Project onto the active arc plane
            double ax, ay, bx, by;
            GetPlaneCoords(from,   center, plane, out ax, out ay);
            GetPlaneCoords(to,     center, plane, out bx, out by);

            double startAngle = Math.Atan2(ay, ax);
            double endAngle   = Math.Atan2(by, bx);

            double sweep = endAngle - startAngle;
            if (clockwise  && sweep > 0) sweep -= 2 * Math.PI;
            if (!clockwise && sweep < 0) sweep += 2 * Math.PI;

            int steps = Math.Max(1, (int)(Math.Abs(sweep) / (2 * Math.PI) * StepsPerFullCircle));
            double dAngle = sweep / steps;
            double dZ     = (to.Z - from.Z) / steps;

            var points = new Point3D[steps + 1];
            points[0] = from;
            for (int n = 1; n <= steps; n++)
            {
                double a = startAngle + n * dAngle;
                points[n] = BuildPoint(center, radius, a, from.Z + n * dZ, plane);
            }
            points[steps] = to; // snap last point to avoid floating-point drift

            for (int n = 0; n < steps; n++)
                yield return new ToolpathSegment
                {
                    From     = points[n],
                    To       = points[n + 1],
                    MoveType = MoveType.Arc
                };
        }

        private static void GetPlaneCoords(Point3D pt, Point3D center, ArcPlane plane,
                                            out double u, out double v)
        {
            switch (plane)
            {
                case ArcPlane.XZ: u = pt.X - center.X; v = pt.Z - center.Z; break;
                case ArcPlane.YZ: u = pt.Y - center.Y; v = pt.Z - center.Z; break;
                default:          u = pt.X - center.X; v = pt.Y - center.Y; break; // XY
            }
        }

        private static Point3D BuildPoint(Point3D center, double radius,
                                          double angle, double z, ArcPlane plane)
        {
            double u = radius * Math.Cos(angle);
            double v = radius * Math.Sin(angle);
            switch (plane)
            {
                case ArcPlane.XZ: return new Point3D(center.X + u, center.Y, center.Z + v);
                case ArcPlane.YZ: return new Point3D(center.X, center.Y + u, center.Z + v);
                default:          return new Point3D(center.X + u, center.Y + v, z);
            }
        }

        private static (Point3D center, double radius) ComputeCenterFromRadius(
            Point3D from, Point3D to, double r, bool clockwise, ArcPlane plane)
        {
            // Chord midpoint
            double mx = (from.X + to.X) / 2;
            double my = (from.Y + to.Y) / 2;

            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double chordLen = Math.Sqrt(dx * dx + dy * dy);

            double h = Math.Sqrt(Math.Max(0, r * r - (chordLen / 2) * (chordLen / 2)));

            // Perpendicular bisector direction
            double px = -dy / chordLen;
            double py =  dx / chordLen;

            // CW vs CCW determines which side of the chord the center sits
            double sign = (clockwise ^ (r < 0)) ? -1 : 1;

            return (new Point3D(mx + sign * h * px, my + sign * h * py, from.Z),
                    Math.Abs(r));
        }
    }
}
