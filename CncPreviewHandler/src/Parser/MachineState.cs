using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    public enum ArcPlane { XY, XZ, YZ }
    public enum Units    { Metric, Inch }

    /// <summary>
    /// Tracks modal machine state across G-code lines.
    /// G-code is stateful: coordinates, motion mode, units, and arc plane
    /// all persist until explicitly changed.
    /// </summary>
    public class MachineState
    {
        public Point3D  Position   { get; set; } = new Point3D(0, 0, 0);
        public int      MotionMode { get; set; } = 0;   // last active G0/G1/G2/G3
        public bool     Absolute   { get; set; } = true; // G90 default
        public ArcPlane Plane      { get; set; } = ArcPlane.XY;
        public Units    Units      { get; set; } = Units.Metric;

        private double _scale => Units == Units.Inch ? 25.4 : 1.0;

        /// <summary>
        /// Resolve the next absolute position from parsed X/Y/Z tokens,
        /// honouring absolute vs incremental mode and unit scaling.
        /// A null token means "no change" (modal coordinates).
        /// </summary>
        public Point3D ResolveTarget(double? x, double? y, double? z)
        {
            double scale = _scale;
            if (Absolute)
            {
                return new Point3D(
                    x.HasValue ? x.Value * scale : Position.X,
                    y.HasValue ? y.Value * scale : Position.Y,
                    z.HasValue ? z.Value * scale : Position.Z);
            }
            else // incremental (G91)
            {
                return new Point3D(
                    Position.X + (x.HasValue ? x.Value * scale : 0),
                    Position.Y + (y.HasValue ? y.Value * scale : 0),
                    Position.Z + (z.HasValue ? z.Value * scale : 0));
            }
        }
    }
}
