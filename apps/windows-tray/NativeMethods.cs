using System.Runtime.InteropServices;

namespace Deluno.Tray;

internal static partial class NativeMethods
{
    internal const int WM_DELUNO_SHOW = 0x8001;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindow(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint hWnd, int msg, nint wParam, nint lParam);
}
