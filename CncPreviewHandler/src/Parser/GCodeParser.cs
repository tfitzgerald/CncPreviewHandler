using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    public class GCodeParser
    {
        private static readonly Regex TokenRegex =
            new Regex(@"([A-Za-z])\s*(-?[\d]*\.?[\d]+)", RegexOptions.Compiled);

        private const int MaxSegments = 40000;
        private const double MinMoveMm = 0.1;

        public List<ToolpathSegment> Parse(string filePath)
        {
            var segments = new List<ToolpathSegment>(MaxSegments);
            var state    = new MachineState();

            var encoding = System.Text.Encoding.GetEncoding(1252,
                new System.Text.EncoderExceptionFallback(),
                new System.Text.DecoderReplacementFallback("?"));

            long totalLines = 0;
            try
            {
                using (var counter = new StreamReader(filePath, encoding))
                {
                    while (counter.ReadLine() != null) totalLines++;
                }
            }
            catch { totalLines = 100000; }

            int stride = totalLines > 300000 ? 4
                       : totalLines > 150000 ? 2
                       : 1;
            int lineIdx = 0;

            foreach (var rawLine in File.ReadLines(filePath, encoding))
            {
                if (segments.Count >= MaxSegments) break;
                lineIdx++;
                try
                {
                    var line = StripComments(rawLine).Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("%") || line == "M30" || line == "M2") continue;
                    if (line.StartsWith("EXCLUDE_") || line.StartsWith("SET_") ||
                        line.StartsWith("TUNING_") || line.StartsWith("PRINT_")) continue;

                    bool hasMotion = line.IndexOf('G') >= 0 || line.IndexOf('X') >= 0 ||
                                     line.IndexOf('Y') >= 0 || line.IndexOf('Z') >= 0;
                    if (hasMotion && stride > 1 && (lineIdx % stride) != 0) continue;

                    ProcessLine(line, state, segments);
                }
                catch { }
            }
            return segments;
        }

        private static string StripComments(string line)
        {
            line = Regex.Replace(line, @"\(.*?\)", "");
            int semi = line.IndexOf(';');
            if (semi >= 0) line = line.Substring(0, semi);
            return line;
        }

        private static Dictionary<char, double> Tokenize(string line)
        {
            var tokens = new Dictionary<char, double>();
            foreach (Match m in TokenRegex.Matches(line.ToUpperInvariant()))
            {
                char   letter = m.Groups[1].Value[0];
                double value  = double.Parse(m.Groups[2].Value);
                tokens[letter] = value;
            }
            return tokens;
        }

        private static void ProcessLine(string line, MachineState state,
                                        List<ToolpathSegment> segments)
        {
            var t = Tokenize(line);
            double? x  = t.ContainsKey('X') ? (double?)t['X'] : null;
            double? y  = t.ContainsKey('Y') ? (double?)t['Y'] : null;
            double? z  = t.ContainsKey('Z') ? (double?)t['Z'] : null;
            double? ii = t.ContainsKey('I') ? (double?)t['I'] : null;
            double? jj = t.ContainsKey('J') ? (double?)t['J'] : null;
            double? kk = t.ContainsKey('K') ? (double?)t['K'] : null;
            double? r  = t.ContainsKey('R') ? (double?)t['R'] : null;

            if (t.ContainsKey('G'))
            {
                int g = (int)t['G'];
                switch (g)
                {
                    case 0: case 1: case 2: case 3: state.MotionMode = g; break;
                    case 17: state.Plane = ArcPlane.XY;  break;
                    case 18: state.Plane = ArcPlane.XZ;  break;
                    case 19: state.Plane = ArcPlane.YZ;  break;
                    case 20: state.Units = Units.Inch;    break;
                    case 21: state.Units = Units.Metric;  break;
                    case 90: state.Absolute = true;       break;
                    case 91: state.Absolute = false;      break;
                    case 81: case 82: case 83: case 84:
                    case 85: case 86: case 87: case 88: case 89:
                        AppendDrillCycle(x, y, z, state, segments); return;
                    case 80: return;
                }
            }

            if (x == null && y == null && z == null) return;

            var target = state.ResolveTarget(x, y, z);
            var delta  = target - state.Position;
            if (delta.Length < MinMoveMm) return;

            switch (state.MotionMode)
            {
                case 0:
                    segments.Add(new ToolpathSegment
                        { From = state.Position, To = target, MoveType = MoveType.Rapid });
                    break;
                case 1:
                    segments.Add(new ToolpathSegment
                        { From = state.Position, To = target, MoveType = MoveType.Cut });
                    break;
                case 2: case 3:
                    segments.AddRange(ArcInterpolator.Expand(
                        state.Position, target, ii, jj, kk, r,
                        clockwise: state.MotionMode == 2,
                        plane: state.Plane));
                    break;
            }
            state.Position = target;
        }

        private static void AppendDrillCycle(double? x, double? y, double? z,
                                             MachineState state,
                                             List<ToolpathSegment> segments)
        {
            var xyPos  = state.ResolveTarget(x, y, state.Position.Z);
            var drillZ = z.HasValue
                ? new Point3D(xyPos.X, xyPos.Y,
                    state.Units == Units.Inch ? z.Value * 25.4 : z.Value)
                : xyPos;
            segments.Add(new ToolpathSegment { From = state.Position, To = xyPos,   MoveType = MoveType.Rapid });
            segments.Add(new ToolpathSegment { From = xyPos,          To = drillZ,  MoveType = MoveType.Cut   });
            segments.Add(new ToolpathSegment { From = drillZ,         To = xyPos,   MoveType = MoveType.Rapid });
            state.Position = xyPos;
        }
    }
}
