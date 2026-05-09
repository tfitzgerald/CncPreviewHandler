using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
            // Walk the entire inheritance chain and dump every field
            var sb = new StringBuilder();
            var type = typeof(SharpPreviewHandler);
            while (type != null && type != typeof(object))
            {
                foreach (var f in type.GetFields(
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    try
                    {
                        var val = f.GetValue(this);
                        sb.AppendLine($"[{type.Name}] {f.FieldType.Name} {f.Name} = {val}");
                    }
                    catch { }
                }
                type = type.BaseType;
            }

            return new CncPreviewControl(sb.ToString());
        }
    }
}
