using System.Reflection;
using System.Runtime.InteropServices;
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
            string path = null;

            // SharpShell 2.7.2 stores the path as SelectedFilePath (auto-property)
            var prop = typeof(SharpPreviewHandler).GetProperty("SelectedFilePath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null) path = prop.GetValue(this) as string;

            if (string.IsNullOrEmpty(path))
            {
                var field = typeof(SharpPreviewHandler).GetField(
                    "<SelectedFilePath>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) path = field.GetValue(this) as string;
            }

            return new CncPreviewControl(path ?? string.Empty);
        }
    }
}
