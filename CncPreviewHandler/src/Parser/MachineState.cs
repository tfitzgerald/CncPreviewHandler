using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    public enum ArcPlane { XY, XZ, YZ }
    public enum Units    { Metric, Inch }

    public class MachineState
    {
        public Point3D  Position   { get; set; } = new Point3D(0, 0, 0);
        public int      MotionMode { get; set; } = 0;
        public bool     Absolute   { get; set; } = true;
        public ArcPlane Plane      { get; set; } = ArcPlane.XY;
        public Units    Units      { get; set; } = Units.Metric;

        public Point3D ResolveTarget(double? x, double? y, double? z)
        {
            double scale = Units == Units.Inch ? 25.4 : 1.0;
            if (Absolute)
            {
                return new Point3D(
                    x.HasValue ? x.Value * scale : Position.X,
                    y.HasValue ? y.Value * scale : Position.Y,
                    z.HasValue ? z.Value * scale : Position.Z);
            }
            return new Point3D(
                Position.X + (x.HasValue ? x.Value * scale : 0),
                Position.Y + (y.HasValue ? y.Value * scale : 0),
                Position.Z + (z.HasValue ? z.Value * scale : 0));
        }
    }
}
