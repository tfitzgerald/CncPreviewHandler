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
            // SharpShell stores the path in a private field after IInitializeWithFile
            // is called internally. Read it via reflection since it is not exposed.
            string filePath = null;

            foreach (var name in new[] { "filePath", "selectedItemPath",
                                         "_filePath",  "m_filePath", "path" })
            {
                var field = typeof(SharpPreviewHandler).GetField(name,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) continue;
                filePath = field.GetValue(this) as string;
                if (!string.IsNullOrEmpty(filePath)) break;
            }

            return new CncPreviewControl(filePath ?? string.Empty);
        }
    }
}
