using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    /// <summary>
    /// COM entry point registered with Windows Explorer.
    /// SharpShell 2.7.2: DoPreview() takes no parameters and returns PreviewHandlerControl.
    /// SelectedItemPath gives the path to the selected file.
    /// </summary>
    [ComVisible(true)]
    [Guid("B1C2D3E4-F5A6-7890-BCDE-F01234567891")]
    [COMServerAssociation(AssociationType.ClassOfExtension,
        ".nc", ".gcode", ".gc", ".g", ".tap", ".cnc")]
    [DisplayName("CNC Toolpath Preview Handler")]
    public class CncPreviewHandlerServer : SharpPreviewHandler
    {
        protected override PreviewHandlerControl DoPreview()
        {
            return new CncPreviewControl(SelectedItemPath);
        }
    }
}
