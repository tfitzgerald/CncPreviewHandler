using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace CncPreviewHandler.Parser
{
    /// <summary>
    /// Parses RS-274 G-code files (.nc, .gcode, .tap, .cnc) into a list of
    /// ToolpathSegments. Handles G0/G1 linear moves, G2/G3 arc moves,
    /// G17-19 plane selection, G20/21 units, G90/91 abs/inc mode,
    /// and G81-89 canned drill cycles (approximated as vertical plunge).
    /// Lines that cannot be parsed are skipped rather than throwing.
    /// Large files are automatically decimated to stay under MaxSegments.
    /// </summary>
    public class GCodeParser
    {
        // Matches a letter followed by an optional sign and number: X-12.345 or G1
        private static readonly Regex TokenRegex =
            new Regex(@"([A-Za-z])\s*(-?[\d]*\.?[\d]+)", RegexOptions.Compiled);

        // Cap on output segments â€” keeps HelixToolkit rendering fast
        private const int MaxSegments = 40_000;

        // Minimum move distance (mm) â€” filters micro-moves that add no visual detail
        private const double MinMoveMm = 0.1;

        public List<ToolpathSegment> Parse(string filePath)
        {
            var segments = new List<ToolpathSegment>(MaxSegments);
            var state    = new MachineState();

            // ANSI-tolerant encoding handles files from any slicer
            var encoding = System.Text.Encoding.GetEncoding(1252,
                new System.Text.EncoderExceptionFallback(),
                new System.Text.DecoderReplacementFallback("?"));

            // Count lines so we can set a decimate stride for huge files
            long totalLines = 0;
            try
            {
                using (var counter = new StreamReader(filePath, encoding);
                while (counter.ReadLine() != null) totalLines++; }
            }
            catch { totalLines = 100_000; }

            // Skip every Nth line for very large files â€” keeps parse time fast
            int stride = totalLines > 300_000 ? 4
                       : totalLines > 150_000 ? 2
                       : 1;
            int lineIdx = 0;

            foreach (var rawLine in File.ReadLines(filePath, encoding))
            {
                if (segments.Count >= MaxSegments) break;

                lineIdx++;
                // Always process non-move lines (G-code state updates)
                // Only decimate actual motion lines
                try
                {
                    var line = StripComments(rawLine).Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    // Skip file-number lines and program-end markers
                    if (line.StartsWith("%") || line == "M30" || line == "M2")
                        continue;

                    // Skip Klipper-specific macros and other non-standard commands
                    if (line.StartsWith("EXCLUDE_") || line.StartsWith("SET_") ||
                        line.StartsWith("TUNING_") || line.StartsWith("PRINT_"))
                        continue;

                    // Apply decimation stride only to motion lines
                    bool hasMotion = line.IndexOf('G') >= 0 || line.IndexOf('X') >= 0 ||
                                     line.IndexOf('Y') >= 0 || line.IndexOf('Z') >= 0;
                    if (hasMotion && stride > 1 && (lineIdx % stride) != 0) continue;

                    ProcessLine(line, state, segments);
                }
                catch { /* skip malformed lines */ }
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
                    case 0: case 1: case 2: case 3:
                        state.MotionMode = g; break;
                    case 17: state.Plane = ArcPlane.XY;   break;
                    case 18: state.Plane = ArcPlane.XZ;   break;
                    case 19: state.Plane = ArcPlane.YZ;   break;
                    case 20: state.Units = Units.Inch;     break;
                    case 21: state.Units = Units.Metric;   break;
                    case 90: state.Absolute = true;        break;
                    case 91: state.Absolute = false;       break;
                    case 81: case 82: case 83: case 84:
                    case 85: case 86: case 87: case 88: case 89:
                        AppendDrillCycle(x, y, z, state, segments); return;
                    case 80: return;
                }
            }

            if (x == null && y == null && z == null) return;

            var target = state.ResolveTarget(x, y, z);

            // Skip micro-moves that add no visual detail
            var delta = target - state.Position;
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
                case 2:
                case 3:
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

            segments.Add(new ToolpathSegment
                { From = state.Position, To = xyPos,   MoveType = MoveType.Rapid });
            segments.Add(new ToolpathSegment
                { From = xyPos,          To = drillZ,  MoveType = MoveType.Cut });
            segments.Add(new ToolpathSegment
                { From = drillZ,         To = xyPos,   MoveType = MoveType.Rapid });

            state.Position = xyPos;
        }
    }
}
