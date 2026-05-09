using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CncPreviewHandler.Parser
{
    /// <summary>
    /// Sniffs the first ~200 lines of a G-code file for slicer/CAM
    /// signatures and returns a human-readable dialect name.
    /// Returns null if no known dialect detected.
    /// </summary>
    public static class DialectDetector
    {
        private static readonly Encoding Enc = Encoding.GetEncoding(1252,
            new EncoderExceptionFallback(),
            new DecoderReplacementFallback("?"));

        public static string Detect(string filePath)
        {
            try
            {
                using (var sr = new StreamReader(filePath, Enc))
                {
                    for (int i = 0; i < 200 && !sr.EndOfStream; i++)
                    {
                        var line = sr.ReadLine();
                        if (line == null) break;
                        var l = line.ToLowerInvariant();

                        // 3D printer slicers
                        if (l.Contains("elegooslicer"))    return "ElegooSlicer";
                        if (l.Contains("bambustudio"))     return "Bambu Studio";
                        if (l.Contains("orcaslicer"))      return "OrcaSlicer";
                        if (l.Contains("prusaslicer"))     return "PrusaSlicer";
                        if (l.Contains("superslicer"))     return "SuperSlicer";
                        if (l.Contains("cura"))            return "Cura";
                        if (l.Contains("simplify3d"))      return "Simplify3D";
                        if (l.Contains("ideamaker"))       return "ideaMaker";
                        if (l.Contains("kissslicer"))      return "KISSlicer";
                        if (l.Contains("klipper"))         return "Klipper";
                        if (l.Contains("marlin"))          return "Marlin";
                        if (l.Contains("pronterface"))     return "Pronterface";

                        // CNC CAM software
                        if (l.Contains("fusion 360") ||
                            l.Contains("fusion cam") ||
                            l.Contains("autodesk fusion")) return "Fusion 360";
                        if (l.Contains("mastercam"))       return "Mastercam";
                        if (l.Contains("solidcam"))        return "SolidCAM";
                        if (l.Contains("nx cam"))          return "Siemens NX";
                        if (l.Contains("powermill"))       return "PowerMill";
                        if (l.Contains("hsmworks"))        return "HSMWorks";
                        if (l.Contains("vcarve") ||
                            l.Contains("vectric"))         return "Vectric";
                        if (l.Contains("carveco"))         return "Carveco";
                        if (l.Contains("easel"))           return "Easel";
                        if (l.Contains("lightburn"))       return "LightBurn";
                        if (l.Contains("kiri:moto") ||
                            l.Contains("kirimoto"))        return "Kiri:Moto";

                        // Fanuc-style: '%' wrapper or O-number near top
                        if (i < 10 &&
                            (line.StartsWith("%") || Regex.IsMatch(line, @"^O\d+")))
                            return "Fanuc-style CNC";
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
