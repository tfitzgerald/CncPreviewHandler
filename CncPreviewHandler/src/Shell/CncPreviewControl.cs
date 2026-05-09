using System.Drawing;
using System.Windows.Forms;
using SharpShell.SharpPreviewHandler;

namespace CncPreviewHandler.Shell
{
    public class CncPreviewControl : PreviewHandlerControl
    {
        public CncPreviewControl(string text)
        {
            var box = new RichTextBox
            {
                Dock      = DockStyle.Fill,
                Text      = text,
                ForeColor = Color.White,
                BackColor = Color.Black,
                Font      = new Font("Consolas", 9f),
                ReadOnly  = true,
                BorderStyle = BorderStyle.None,
            };
            Controls.Add(box);
        }
    }
}
