using System.Drawing;
using System.Windows.Forms;
using CncPreviewHandler.Parser;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    public class CncPreviewControl : PreviewHandlerControl
    {
        public CncPreviewControl(string filePath)
        {
            BackColor = Color.Black;

            var label = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                BackColor = Color.Black,
                Font      = new Font("Segoe UI", 11f),
                Text      = string.IsNullOrEmpty(filePath)
                            ? "ERROR: No file path received from Explorer"
                            : $"Handler OK\n{filePath}\n\nParsing..."
            };
            Controls.Add(label);

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var segments = new GCodeParser().Parse(filePath);
                label.Text = $"Parsed OK\n{filePath}\n\n{segments.Count} moves found\n\n(WPF viewport coming next)";
            }
            catch (System.Exception ex)
            {
                label.Text      = $"Parse error:\n{ex.Message}";
                label.ForeColor = Color.OrangeRed;
            }
        }
    }
}
