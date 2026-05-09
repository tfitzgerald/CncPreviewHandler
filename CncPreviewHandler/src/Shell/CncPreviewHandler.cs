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
            string filePath = null;
            var prop = typeof(SharpPreviewHandler).GetProperty("SelectedFilePath",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) filePath = prop.GetValue(this) as string;
            if (string.IsNullOrEmpty(filePath))
            {
                var field = typeof(SharpPreviewHandler).GetField(
                    "<SelectedFilePath>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) filePath = field.GetValue(this) as string;
            }
            return new CncPreviewControl(filePath ?? string.Empty);
        }
    }
}
