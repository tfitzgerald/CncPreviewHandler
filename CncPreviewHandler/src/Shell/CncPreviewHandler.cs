using System.Runtime.InteropServices;
using SharpShell.Attributes;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    [ComVisible(true)]
    [Guid("B1C2D3E4-F5A6-7890-BCDE-F01234567891")]
    [COMServerAssociation(AssociationType.ClassOfExtension,
        ".nc", ".gcode", ".gc", ".g", ".tap", ".cnc")]
    [PreviewHandler("CNC Toolpath Preview Handler", ".nc", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567892}")]
    [DisplayName("CNC Toolpath Preview Handler")]
    public class CncPreviewHandlerServer : SharpPreviewHandler, IInitializeWithFile
    {
        private string _filePath;

        void IInitializeWithFile.Initialize(string pszFilePath, uint grfMode)
        {
            _filePath = pszFilePath;
        }

        protected override PreviewHandlerControl DoPreview()
        {
            return new CncPreviewControl(_filePath ?? string.Empty);
        }
    }
}
