using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
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
            string source = "none";

            try
            {
                var prop = typeof(SharpPreviewHandler).GetProperty("SelectedFilePath",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) { path = prop.GetValue(this) as string; source = "property"; }
            }
            catch { }

            if (string.IsNullOrEmpty(path))
            {
                try
                {
                    var field = typeof(SharpPreviewHandler).GetField(
                        "<SelectedFilePath>k__BackingField",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) { path = field.GetValue(this) as string; source = "field"; }
                }
                catch { }
            }

            // Show diagnostic info
            string ext    = string.IsNullOrEmpty(path) ? "N/A" : Path.GetExtension(path);
            string exists = string.IsNullOrEmpty(path) ? "N/A" : File.Exists(path).ToString();
            string attrs  = "N/A";
            try { attrs = File.GetAttributes(path).ToString(); } catch { }

            var diag = new DiagControl(
                $"Path:   {path ?? "(null)"}\n" +
                $"Source: {source}\n" +
                $"Ext:    {ext}\n" +
                $"Exists: {exists}\n" +
                $"Attrs:  {attrs}");

            // Only proceed to real preview if we have a usable path
            if (string.IsNullOrEmpty(path)) return diag;

            return new CncPreviewControl(path);
        }
    }

    // Temporary diagnostic control
    class DiagControl : PreviewHandlerControl
    {
        public DiagControl(string text)
        {
            BackColor = Color.FromArgb(20, 20, 20);
            Controls.Add(new Label
            {
                Dock      = DockStyle.Fill,
                Text      = text,
                ForeColor = Color.Yellow,
                BackColor = Color.FromArgb(20, 20, 20),
                Font      = new Font("Consolas", 10f),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding   = new Padding(20)
            });
        }
    }
}
