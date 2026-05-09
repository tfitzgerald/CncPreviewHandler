using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CncPreviewHandler.Diagnostics;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    [ComVisible(true)]
    [Guid("B1C2D3E4-F5A6-7890-BCDE-F01234567891")]
    [COMServerAssociation(AssociationType.ClassOfExtension,
        ".nc", ".gcode", ".gc", ".g", ".tap", ".cnc")]
    [DisplayName("CNC Toolpath Preview Handler")]
    [PreviewHandler]
    public class CncPreviewHandlerServer : SharpPreviewHandler
    {
        protected override PreviewHandlerControl DoPreview()
        {
            try
            {
                string path = ResolveSelectedFilePath();
                Log.Info($"DoPreview called: path='{path ?? "(null)"}'");

                if (string.IsNullOrEmpty(path))
                {
                    Log.Warn("DoPreview: no file path resolved from SharpShell");
                    return new CncPreviewControl(string.Empty);
                }

                if (!File.Exists(path))
                {
                    Log.Warn($"DoPreview: file does not exist: {path}");
                }

                return new CncPreviewControl(path);
            }
            catch (Exception ex)
            {
                Log.Error("DoPreview threw", ex);
                return new CncPreviewControl(string.Empty);
            }
        }

        private string ResolveSelectedFilePath()
        {
            try
            {
                var prop = typeof(SharpPreviewHandler).GetProperty("SelectedFilePath",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                {
                    var v = prop.GetValue(this) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            catch (Exception ex) { Log.Warn("SelectedFilePath property access failed: " + ex.Message); }

            try
            {
                var field = typeof(SharpPreviewHandler).GetField(
                    "<SelectedFilePath>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(this) as string;
            }
            catch (Exception ex) { Log.Warn("SelectedFilePath field access failed: " + ex.Message); }

            return null;
        }
    }
}
