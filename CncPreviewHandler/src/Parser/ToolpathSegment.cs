namespace CncPreviewHandler.Parser
{
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X=x; Y=y; Z=z; }
        public static Vec3 operator-(Vec3 a, Vec3 b) => new Vec3(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
        public double Length => System.Math.Sqrt(X*X + Y*Y + Z*Z);
        public static readonly Vec3 Zero = new Vec3(0,0,0);
    }

    public enum MoveType { Rapid, Cut, Arc }

    public class ToolpathSegment
    {
        public Vec3     From     { get; set; }
        public Vec3     To       { get; set; }
        public MoveType MoveType { get; set; }
    }
}
