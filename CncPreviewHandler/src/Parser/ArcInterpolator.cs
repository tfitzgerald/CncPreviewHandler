using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    public static class ArcInterpolator
    {
        private const int Steps = 36;

        public static IEnumerable<ToolpathSegment> Expand(
            Point3D from, Point3D to,
            double? i, double? j, double? k, double? r,
            bool clockwise, ArcPlane plane)
        {
            Point3D center;
            double  radius;

            if (r.HasValue)
            {
                var result = CenterFromRadius(from, to, r.Value, clockwise, plane);
                center = result.Item1;
                radius = result.Item2;
            }
            else
            {
                double ci = i ?? 0, cj = j ?? 0, ck = k ?? 0;
                center = new Point3D(from.X + ci, from.Y + cj, from.Z + ck);
                radius = Math.Sqrt(ci * ci + cj * cj + ck * ck);
            }

            double ax, ay, bx, by;
            PlaneCoords(from, center, plane, out ax, out ay);
            PlaneCoords(to,   center, plane, out bx, out by);

            double startAngle = Math.Atan2(ay, ax);
            double endAngle   = Math.Atan2(by, bx);
            double sweep      = endAngle - startAngle;
            if (clockwise  && sweep > 0) sweep -= 2 * Math.PI;
            if (!clockwise && sweep < 0) sweep += 2 * Math.PI;

            int steps  = Math.Max(1, (int)(Math.Abs(sweep) / (2 * Math.PI) * Steps));
            double dA  = sweep / steps;
            double dZ  = (to.Z - from.Z) / steps;

            var pts = new Point3D[steps + 1];
            pts[0] = from;
            for (int n = 1; n <= steps; n++)
            {
                double a = startAngle + n * dA;
                pts[n] = BuildPt(center, radius, a, from.Z + n * dZ, plane);
            }
            pts[steps] = to;

            for (int n = 0; n < steps; n++)
                yield return new ToolpathSegment
                    { From = pts[n], To = pts[n + 1], MoveType = MoveType.Arc };
        }

        static void PlaneCoords(Point3D pt, Point3D cen, ArcPlane pl,
                                out double u, out double v)
        {
            switch (pl)
            {
                case ArcPlane.XZ: u = pt.X - cen.X; v = pt.Z - cen.Z; break;
                case ArcPlane.YZ: u = pt.Y - cen.Y; v = pt.Z - cen.Z; break;
                default:          u = pt.X - cen.X; v = pt.Y - cen.Y; break;
            }
        }

        static Point3D BuildPt(Point3D cen, double r, double a, double z, ArcPlane pl)
        {
            double u = r * Math.Cos(a), v = r * Math.Sin(a);
            switch (pl)
            {
                case ArcPlane.XZ: return new Point3D(cen.X + u, cen.Y,     cen.Z + v);
                case ArcPlane.YZ: return new Point3D(cen.X,     cen.Y + u, cen.Z + v);
                default:          return new Point3D(cen.X + u, cen.Y + v, z);
            }
        }

        static Tuple<Point3D, double> CenterFromRadius(
            Point3D from, Point3D to, double r, bool cw, ArcPlane pl)
        {
            double mx = (from.X + to.X) / 2;
            double my = (from.Y + to.Y) / 2;
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            double h = Math.Sqrt(Math.Max(0, r * r - (chord / 2) * (chord / 2)));
            double px = -dy / chord, py = dx / chord;
            double sign = (cw ^ (r < 0)) ? -1 : 1;
            return Tuple.Create(
                new Point3D(mx + sign * h * px, my + sign * h * py, from.Z),
                Math.Abs(r));
        }
    }
}
