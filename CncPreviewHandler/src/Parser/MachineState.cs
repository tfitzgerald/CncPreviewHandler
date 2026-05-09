namespace CncPreviewHandler.Parser
{
    public enum ArcPlane { XY, XZ, YZ }
    public enum Units    { Metric, Inch }

    public class MachineState
    {
        public Vec3     Position   { get; set; } = Vec3.Zero;
        public int      MotionMode { get; set; } = 0;
        public bool     Absolute   { get; set; } = true;
        public ArcPlane Plane      { get; set; } = ArcPlane.XY;
        public Units    Units      { get; set; } = Units.Metric;

        public Vec3 ResolveTarget(double? x, double? y, double? z)
        {
            double s = Units == Units.Inch ? 25.4 : 1.0;
            if (Absolute)
                return new Vec3(
                    x.HasValue ? x.Value*s : Position.X,
                    y.HasValue ? y.Value*s : Position.Y,
                    z.HasValue ? z.Value*s : Position.Z);
            return new Vec3(
                Position.X + (x.HasValue ? x.Value*s : 0),
                Position.Y + (y.HasValue ? y.Value*s : 0),
                Position.Z + (z.HasValue ? z.Value*s : 0));
        }
    }
}
