using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace CncPreviewHandler.Parser
{
    public class GCodeParser
    {
        private static readonly Regex TokenRe =
            new Regex(@"([A-Za-z])\s*(-?[\d]*\.?[\d]+)", RegexOptions.Compiled);

        private const int    MaxSegs   = 40000;
        private const double MinMoveMm = 0.1;
        private const double LayerStepMm = 0.05;   // min Z increase to count as new layer

        public List<ToolpathSegment> Parse(string filePath)
        {
            var segs  = new List<ToolpathSegment>(MaxSegs);
            var state = new MachineState();
            var enc   = System.Text.Encoding.GetEncoding(1252,
                new System.Text.EncoderExceptionFallback(),
                new System.Text.DecoderReplacementFallback("?"));

            long total = 0;
            try { using (var sr = new StreamReader(filePath, enc))
                      while (sr.ReadLine() != null) total++; }
            catch { total = 100000; }

            int stride = total > 300000 ? 4 : total > 150000 ? 2 : 1;
            int idx    = 0;

            foreach (var raw in File.ReadLines(filePath, enc))
            {
                if (segs.Count >= MaxSegs) break;
                idx++;
                try
                {
                    var line = Strip(raw).Trim();
                    if (line.Length == 0) continue;
                    if (line[0]=='%' || line=="M30" || line=="M2") continue;
                    if (line.StartsWith("EXCLUDE_") || line.StartsWith("SET_") ||
                        line.StartsWith("TUNING_")  || line.StartsWith("PRINT_")) continue;
                    bool hasMotion = line.IndexOfAny(new[]{'G','X','Y','Z','F','g','x','y','z','f'}) >= 0;
                    if (hasMotion && stride>1 && (idx%stride)!=0) continue;
                    ProcessLine(line, state, segs);
                }
                catch { }
            }

            AssignLayers(segs);
            return segs;
        }

        static string Strip(string line)
        {
            line = Regex.Replace(line, @"\(.*?\)", "");
            int s = line.IndexOf(';');
            if (s >= 0) line = line.Substring(0, s);
            return line;
        }

        static Dictionary<char,double> Tok(string line)
        {
            var d = new Dictionary<char,double>();
            foreach (Match m in TokenRe.Matches(line.ToUpperInvariant()))
            {
                char c = m.Groups[1].Value[0];
                if (double.TryParse(m.Groups[2].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                    d[c] = v;
            }
            return d;
        }

        static void ProcessLine(string line, MachineState st, List<ToolpathSegment> segs)
        {
            var t  = Tok(line);
            double? x =t.ContainsKey('X')?(double?)t['X']:null;
            double? y =t.ContainsKey('Y')?(double?)t['Y']:null;
            double? z =t.ContainsKey('Z')?(double?)t['Z']:null;
            double? ii=t.ContainsKey('I')?(double?)t['I']:null;
            double? jj=t.ContainsKey('J')?(double?)t['J'] : null;
            double? kk=t.ContainsKey('K')?(double?)t['K']:null;
            double? r =t.ContainsKey('R')?(double?)t['R']:null;

            // Track feedrate (F word)
            if (t.ContainsKey('F')) st.Feedrate = t['F'];

            if (t.ContainsKey('G'))
            {
                int g=(int)t['G'];
                switch(g)
                {
                    case 0:case 1:case 2:case 3: st.MotionMode=g; break;
                    case 17: st.Plane=ArcPlane.XY;  break;
                    case 18: st.Plane=ArcPlane.XZ;  break;
                    case 19: st.Plane=ArcPlane.YZ;  break;
                    case 20: st.Units=Units.Inch;    break;
                    case 21: st.Units=Units.Metric;  break;
                    case 90: st.Absolute=true;       break;
                    case 91: st.Absolute=false;      break;
                    case 80: return;
                    case 81:case 82:case 83:case 84:
                    case 85:case 86:case 87:case 88:case 89:
                        Drill(x,y,z,st,segs); return;
                }
            }

            if (x==null && y==null && z==null) return;
            var tgt   = st.ResolveTarget(x,y,z);
            var delta = tgt - st.Position;
            if (delta.Length < MinMoveMm) { st.Position=tgt; return; }

            switch (st.MotionMode)
            {
                case 0: segs.Add(new ToolpathSegment{From=st.Position,To=tgt,MoveType=MoveType.Rapid,FeedrateMmPerMin=st.Feedrate}); break;
                case 1: segs.Add(new ToolpathSegment{From=st.Position,To=tgt,MoveType=MoveType.Cut,  FeedrateMmPerMin=st.Feedrate}); break;
                case 2: case 3:
                    segs.AddRange(ArcInterpolator.Expand(st.Position,tgt,ii,jj,kk,r,
                        clockwise:st.MotionMode==2, plane:st.Plane, feedrate:st.Feedrate)); break;
            }
            st.Position = tgt;
        }

        static void Drill(double? x, double? y, double? z,
                          MachineState st, List<ToolpathSegment> segs)
        {
            var xy = st.ResolveTarget(x, y, st.Position.Z);
            var dz = z.HasValue
                ? new Vec3(xy.X, xy.Y, st.Units==Units.Inch?z.Value*25.4:z.Value) : xy;
            double f = st.Feedrate;
            segs.Add(new ToolpathSegment{From=st.Position,To=xy, MoveType=MoveType.Rapid,FeedrateMmPerMin=f});
            segs.Add(new ToolpathSegment{From=xy,         To=dz, MoveType=MoveType.Cut,  FeedrateMmPerMin=f});
            segs.Add(new ToolpathSegment{From=dz,         To=xy, MoveType=MoveType.Rapid,FeedrateMmPerMin=f});
            st.Position = xy;
        }

        // Assigns LayerIndex to each segment: increments whenever max-Z rises by
        // LayerStepMm or more above the current high-water mark. For 3D printer
        // files this gives true layer counts; for CNC files (Z bobs up/down) it
        // returns 1 because Z never persistently rises.
        private static void AssignLayers(List<ToolpathSegment> segs)
        {
            if (segs.Count == 0) return;
            int layer = 0;
            double topZ = segs[0].From.Z;
            for (int i = 0; i < segs.Count; i++)
            {
                var s = segs[i];
                double z = Math.Max(s.From.Z, s.To.Z);
                if (z > topZ + LayerStepMm)
                {
                    layer++;
                    topZ = z;
                }
                s.LayerIndex = layer;
            }
        }
    }
}
