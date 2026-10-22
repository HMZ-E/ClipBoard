using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipBoardWindows
{
    internal static class Native
    {
        public const int ClipboardUpdate = 0x031D, Hotkey = 0x0312;
        [DllImport("user32.dll", SetLastError = true)] public static extern bool AddClipboardFormatListener(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RemoveClipboardFormatListener(IntPtr window);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] public static extern bool CloseClipboard();
        [DllImport("user32.dll")] public static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterClipboardFormat(string name);
        [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr handle);
        [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr handle);
        [DllImport("kernel32.dll")] public static extern UIntPtr GlobalSize(IntPtr handle);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        public static HwndSource CreateHost()
        {
            var p = new HwndSourceParameters("ClipBoard.Windows.Host"); p.WindowStyle = unchecked((int)0x80000000); p.Width = 0; p.Height = 0;
            return new HwndSource(p);
        }
    }
}
