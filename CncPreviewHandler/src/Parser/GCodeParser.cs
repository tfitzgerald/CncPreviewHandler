using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CncPreviewHandler.Parser
{
    /// <summary>
    /// Parses RS-274 G-code files (.nc, .gcode, .tap, .cnc) into a list of
    /// ToolpathSegments. Handles G0/G1 linear moves, G2/G3 arc moves,
    /// G17-19 plane selection, G20/21 units, G90/91 abs/inc mode,
    /// and G81-89 canned drill cycles (approximated as vertical plunge).
    /// Lines that cannot be parsed are skipped rather than throwing.
    /// </summary>
    public class GCodeParser
    {
        // Matches a letter followed by an optional sign and number: X-12.345 or G1
        private static readonly Regex TokenRegex =
            new Regex(@"([A-Za-z])\s*(-?[\d]*\.?[\d]+)", RegexOptions.Compiled);

        public List<ToolpathSegment> Parse(string filePath)
        {
            var segments = new List<ToolpathSegment>();
            var state    = new MachineState();

            foreach (var rawLine in File.ReadLines(filePath))
            {
                try
                {
                    var line = StripComments(rawLine).Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Skip file-number lines (% or :nnn) and program-end markers
                    if (line.StartsWith("%") || line.StartsWith("O") || line == "M30" || line == "M2")
                        continue;

                    ProcessLine(line, state, segments);
                }
                catch { /* skip malformed lines */ }
            }

            return segments;
        }

        private static string StripComments(string line)
        {
            // Remove parenthesised comments: (this is a comment)
            line = Regex.Replace(line, @"\(.*?\)", "");
            // Remove semicolon comments: ; rest of line
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
                tokens[letter] = value; // last value wins on duplicate letters
            }
            return tokens;
        }

        private static void ProcessLine(string line, MachineState state,
                                         List<ToolpathSegment> segments)
        {
            var t = Tokenize(line);

            double? x = t.ContainsKey('X') ? (double?)t['X'] : null;
            double? y = t.ContainsKey('Y') ? (double?)t['Y'] : null;
            double? z = t.ContainsKey('Z') ? (double?)t['Z'] : null;
            double? ii = t.ContainsKey('I') ? (double?)t['I'] : null;
            double? jj = t.ContainsKey('J') ? (double?)t['J'] : null;
            double? kk = t.ContainsKey('K') ? (double?)t['K'] : null;
            double? r  = t.ContainsKey('R') ? (double?)t['R'] : null;

            // Update modal G-code if this line specifies one
            if (t.ContainsKey('G'))
            {
                int g = (int)t['G'];
                switch (g)
                {
                    // Motion modes — remembered for subsequent lines
                    case 0: case 1: case 2: case 3:
                        state.MotionMode = g;
                        break;

                    // Arc plane selection
                    case 17: state.Plane = ArcPlane.XY;  break;
                    case 18: state.Plane = ArcPlane.XZ;  break;
                    case 19: state.Plane = ArcPlane.YZ;  break;

                    // Units
                    case 20: state.Units = Units.Inch;   break;
                    case 21: state.Units = Units.Metric; break;

                    // Absolute / incremental
                    case 90: state.Absolute = true;      break;
                    case 91: state.Absolute = false;     break;

                    // Canned drill cycles — treat as a plunge to Z then retract
                    case 81: case 82: case 83: case 84:
                    case 85: case 86: case 87: case 88: case 89:
                        AppendDrillCycle(x, y, z, state, segments);
                        return;

                    // Canned cycle cancel — no geometry
                    case 80: return;
                }
            }

            // Only generate geometry if there are coordinate words on this line
            if (x == null && y == null && z == null) return;

            var target = state.ResolveTarget(x, y, z);

            // Avoid zero-length segments
            if (target == state.Position) return;

            switch (state.MotionMode)
            {
                case 0: // G0 rapid positioning
                    segments.Add(new ToolpathSegment
                    {
                        From = state.Position, To = target, MoveType = MoveType.Rapid
                    });
                    break;

                case 1: // G1 linear interpolation
                    segments.Add(new ToolpathSegment
                    {
                        From = state.Position, To = target, MoveType = MoveType.Cut
                    });
                    break;

                case 2: // G2 clockwise arc
                case 3: // G3 counter-clockwise arc
                    segments.AddRange(ArcInterpolator.Expand(
                        state.Position, target,
                        ii, jj, kk, r,
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
            // Rapid to XY position, then plunge to Z (cut move), then retract
            var xyPos  = state.ResolveTarget(x, y, state.Position.Z);
            var drillZ = z.HasValue
                ? new System.Windows.Media.Media3D.Point3D(xyPos.X, xyPos.Y,
                    state.Units == Units.Inch ? z.Value * 25.4 : z.Value)
                : xyPos;

            segments.Add(new ToolpathSegment
                { From = state.Position, To = xyPos, MoveType = MoveType.Rapid });
            segments.Add(new ToolpathSegment
                { From = xyPos, To = drillZ, MoveType = MoveType.Cut });
            segments.Add(new ToolpathSegment
                { From = drillZ, To = xyPos, MoveType = MoveType.Rapid });

            state.Position = xyPos;
        }
    }
}
