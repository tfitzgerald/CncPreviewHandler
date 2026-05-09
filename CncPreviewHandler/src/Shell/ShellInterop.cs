using System.Runtime.InteropServices;

namespace CncPreviewHandler.Shell
{
    // Win32 IInitializeWithFile — lets us capture the file path directly
    // from the shell, bypassing SharpShell's inaccessible SelectedItemPath.
    [ComImport]
    [Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IInitializeWithFile
    {
        void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }
}
