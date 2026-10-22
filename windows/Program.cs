using System;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ClipBoardWindows
{
    internal static class Program
    {
        internal static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HMZE", "ClipBoard");
        internal static uint ShowMessage = Native.RegisterWindowMessage("HMZE.ClipBoard.Windows.Show.1");
        internal static uint QuitMessage = Native.RegisterWindowMessage("HMZE.ClipBoard.Windows.Quit.1");
        [STAThread] static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : "artifacts");
            if (args.Length > 0 && args[0] == "--quit") { Native.PostMessage(new IntPtr(0xffff), QuitMessage, IntPtr.Zero, IntPtr.Zero); return 0; }
            bool created;
            using (var instance = new Mutex(true, "Local\\HMZE.ClipBoard." + WindowsIdentity.GetCurrent().User.Value, out created)) {
                if (!created) { Native.PostMessage(new IntPtr(0xffff), ShowMessage, IntPtr.Zero, IntPtr.Zero); return 0; }
                try {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
                        MessageBox.Show("ClipBoard encountered an unexpected error and will close. Your saved history stays in the local data folder.\n\n" + e.Exception.GetType().Name, "ClipBoard");
                        e.Handled = true; app.Shutdown(1);
                    };
                    var store = new HistoryStore(Path.Combine(DataFolder, "history.dat"));
                    using (var service = new ClipboardService(store, new WindowsClipboard()))
                    using (var host = Native.CreateHost())
                    using (var tray = new Forms.NotifyIcon()) {
                        var window = new MainWindow(store, service);
                        bool listening = Native.AddClipboardFormatListener(host.Handle);
                        bool hotkey = Native.RegisterHotKey(host.Handle, 1, 0x4006, 0x56); // Ctrl + Shift + V, no repeat
                        host.AddHook(delegate(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled) {
                            if (msg == Native.ClipboardUpdate) { service.Changed(); handled = true; }
                            else if (msg == Native.Hotkey) { window.Toggle(); handled = true; }
                            else if ((uint)msg == ShowMessage) { window.ShowPanel(); handled = true; }
                            else if ((uint)msg == QuitMessage) { app.Shutdown(); handled = true; }
                            return IntPtr.Zero;
                        });
                        tray.Icon = CreateTrayIcon(); tray.Text = "ClipBoard · Ctrl+Shift+V";
                        var menu = new Forms.ContextMenuStrip();
                        menu.Items.Add("Open ClipBoard", null, delegate { window.ShowPanel(); });
                        var pause = new Forms.ToolStripMenuItem("Pause capture") { CheckOnClick = true, Checked = store.State.Settings.Paused };
                        pause.CheckedChanged += delegate { store.State.Settings.Paused = pause.Checked; service.SkipCurrent(); store.UpdateSettings(); };
                        store.Changed += delegate { pause.Checked = store.State.Settings.Paused; };
                        menu.Items.Add(pause);
                        menu.Items.Add("Settings", null, delegate { window.ShowPanel(); window.ShowSettings(); });
                        menu.Items.Add(new Forms.ToolStripSeparator());
                        menu.Items.Add("Quit ClipBoard", null, delegate { app.Shutdown(); });
                        tray.ContextMenuStrip = menu;
                        tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) window.Toggle(); };
                        tray.Visible = true;
                        app.Exit += delegate { tray.Visible = false; Native.UnregisterHotKey(host.Handle, 1); if (listening) Native.RemoveClipboardFormatListener(host.Handle); };
                        if (Array.IndexOf(args, "--background") < 0) window.ShowPanel();
                        if (!listening) window.SetFeedback("Clipboard monitoring could not start. Restart ClipBoard.");
                        else if (!hotkey) window.SetFeedback("Ctrl+Shift+V is in use. Open ClipBoard from its tray icon.");
                        app.Run(); tray.Icon.Dispose(); menu.Dispose();
                    }
                } catch (Exception ex) { MessageBox.Show("ClipBoard could not start: " + ex.Message, "ClipBoard"); return 1; }
                finally { instance.ReleaseMutex(); }
            }
            return 0;
        }
        static Icon CreateTrayIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bitmap)) {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(128, 203, 196), 2.8f)) {
                    g.DrawRectangle(pen, 7, 6, 18, 22); g.DrawRectangle(pen, 12, 3, 8, 6);
                    g.DrawLine(pen, 11, 14, 21, 14); g.DrawLine(pen, 11, 19, 21, 19); g.DrawLine(pen, 11, 24, 18, 24);
                }
                IntPtr icon = bitmap.GetHicon();
                try { return (Icon)Icon.FromHandle(icon).Clone(); } finally { Native.DestroyIcon(icon); }
            }
        }
        internal static bool StartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && key.GetValue("HMZE.ClipBoard") != null;
        }
        internal static void SetStartup(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if (enabled) key.SetValue("HMZE.ClipBoard", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --background");
                else key.DeleteValue("HMZE.ClipBoard", false);
            }
        }
    }
}
