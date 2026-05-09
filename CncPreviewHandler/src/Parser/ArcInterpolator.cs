using System;
using System.Collections.Generic;

namespace CncPreviewHandler.Parser
{
    public static class ArcInterpolator
    {
        private const int Steps = 36;

        public static IEnumerable<ToolpathSegment> Expand(
            Vec3 from, Vec3 to, double? i, double? j, double? k, double? r,
            bool clockwise, ArcPlane plane)
        {
            Vec3   center;
            double radius;
            if (r.HasValue)
            {
                var t = CenterFromRadius(from, to, r.Value, clockwise);
                center = t.Item1; radius = t.Item2;
            }
            else
            {
                double ci=i??0, cj=j??0, ck=k??0;
                center = new Vec3(from.X+ci, from.Y+cj, from.Z+ck);
                radius = Math.Sqrt(ci*ci+cj*cj+ck*ck);
            }

            double ax, ay, bx, by;
            PlaneCoords(from, center, plane, out ax, out ay);
            PlaneCoords(to,   center, plane, out bx, out by);
            double sa = Math.Atan2(ay, ax), ea = Math.Atan2(by, bx);
            double sw = ea - sa;
            if ( clockwise && sw > 0) sw -= 2*Math.PI;
            if (!clockwise && sw < 0) sw += 2*Math.PI;
            int steps = Math.Max(1,(int)(Math.Abs(sw)/(2*Math.PI)*Steps));
            double dA = sw/steps, dZ = (to.Z-from.Z)/steps;
            var pts = new Vec3[steps+1];
            pts[0] = from;
            for (int n=1; n<=steps; n++)
            {
                double a = sa+n*dA;
                pts[n] = BuildPt(center, radius, a, from.Z+n*dZ, plane);
            }
            pts[steps] = to;
            for (int n=0; n<steps; n++)
                yield return new ToolpathSegment { From=pts[n], To=pts[n+1], MoveType=MoveType.Arc };
        }

        static void PlaneCoords(Vec3 p, Vec3 c, ArcPlane pl, out double u, out double v)
        {
            switch(pl)
            {
                case ArcPlane.XZ: u=p.X-c.X; v=p.Z-c.Z; break;
                case ArcPlane.YZ: u=p.Y-c.Y; v=p.Z-c.Z; break;
                default:          u=p.X-c.X; v=p.Y-c.Y; break;
            }
        }

        static Vec3 BuildPt(Vec3 c, double r, double a, double z, ArcPlane pl)
        {
            double u=r*Math.Cos(a), v=r*Math.Sin(a);
            switch(pl)
            {
                case ArcPlane.XZ: return new Vec3(c.X+u, c.Y,   c.Z+v);
                case ArcPlane.YZ: return new Vec3(c.X,   c.Y+u, c.Z+v);
                default:          return new Vec3(c.X+u, c.Y+v, z);
            }
        }

        static Tuple<Vec3,double> CenterFromRadius(Vec3 from, Vec3 to, double r, bool cw)
        {
            double mx=(from.X+to.X)/2, my=(from.Y+to.Y)/2;
            double dx=to.X-from.X, dy=to.Y-from.Y;
            double chord=Math.Sqrt(dx*dx+dy*dy);
            double h=Math.Sqrt(Math.Max(0,r*r-(chord/2)*(chord/2)));
            double px=-dy/chord, py=dx/chord;
            double sign=(cw^(r<0))?-1:1;
            return Tuple.Create(new Vec3(mx+sign*h*px, my+sign*h*py, from.Z), Math.Abs(r));
        }
    }
}
