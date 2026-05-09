using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    public enum MoveType { Rapid, Cut, Arc }

    public class ToolpathSegment
    {
        public Point3D  From     { get; set; }
        public Point3D  To       { get; set; }
        public MoveType MoveType { get; set; }
    }
}
