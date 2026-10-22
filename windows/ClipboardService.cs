using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipBoardWindows
{
    public interface IClipboardAccess
    {
        uint Sequence { get; }
        Clip Read();
        void Write(Clip clip);
    }
    public sealed class WindowsClipboard : IClipboardAccess
    {
        public uint Sequence { get { return Native.GetClipboardSequenceNumber(); } }
        // Password managers can mark a copy as unsuitable for clipboard history.
        bool Excluded()
        {
            uint exclude = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
            if (Native.IsClipboardFormatAvailable(exclude)) return true;
            uint history = Native.RegisterClipboardFormat("CanIncludeInClipboardHistory");
            if (!Native.IsClipboardFormatAvailable(history)) return false;
            if (!Native.OpenClipboard(IntPtr.Zero)) throw new ExternalException("Clipboard is busy");
            try {
                IntPtr handle = Native.GetClipboardData(history);
                if (handle == IntPtr.Zero || Native.GlobalSize(handle).ToUInt64() < 4) return false;
                IntPtr data = Native.GlobalLock(handle);
                if (data == IntPtr.Zero) return false;
                try { return Marshal.ReadInt32(data) == 0; } finally { Native.GlobalUnlock(handle); }
            } finally { Native.CloseClipboard(); }
        }
        public Clip Read()
        {
            if (Excluded()) return null;
            if (Clipboard.ContainsImage()) {
                BitmapSource bitmap = Clipboard.GetImage(); if (bitmap == null) return null;
                if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 25000000) return Clip.FromImage(new byte[HistoryStore.MaxImageBytes + 1]);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = new MemoryStream()) { png.Save(stream); return Clip.FromImage(stream.ToArray()); }
            }
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) return Clip.FromText(Clipboard.GetText(TextDataFormat.UnicodeText));
            return null;
        }
        public void Write(Clip clip)
        {
            var data = new DataObject();
            if (clip.IsImage) { data.SetImage(clip.Image(0)); data.SetData("PNG", new MemoryStream(clip.Png)); }
            else data.SetText(clip.Text, TextDataFormat.UnicodeText);
            Clipboard.SetDataObject(data, true);
        }
    }
    public sealed class ClipboardService : IDisposable
    {
        readonly HistoryStore store;
        readonly IClipboardAccess clipboard;
        readonly DispatcherTimer retry;
        uint sequence;
        int attempts;
        public event Action<string> Feedback;
        public ClipboardService(HistoryStore history, IClipboardAccess access)
        {
            store = history; clipboard = access; sequence = clipboard.Sequence;
            retry = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            retry.Tick += delegate { Capture(); };
        }
        public void Changed() { attempts = 0; Capture(); }
        public void SkipCurrent() { sequence = clipboard.Sequence; retry.Stop(); }
        public void Capture()
        {
            if (store.State.Settings.Paused) { SkipCurrent(); return; }
            uint before = clipboard.Sequence;
            if (before == sequence) { retry.Stop(); return; }
            try {
                Clip clip = clipboard.Read();
                // Delayed clipboard rendering can advance the sequence during Read. Retry a stable snapshot.
                if (before != clipboard.Sequence) { Retry(); return; }
                sequence = before; retry.Stop(); store.Add(clip);
            } catch (ExternalException) { Retry(); }
              catch (IOException) { sequence = before; retry.Stop(); Report("This clipboard image could not be read."); }
              catch (NotSupportedException) { sequence = before; retry.Stop(); }
              catch (ArgumentException) { sequence = before; retry.Stop(); }
              catch (InvalidOperationException) { Retry(); }
        }
        void Retry()
        {
            if (++attempts >= 10) { retry.Stop(); Report("Clipboard is busy. Copy the item again to save it."); }
            else retry.Start();
        }
        public bool Copy(Clip clip)
        {
            try { clipboard.Write(clip); sequence = clipboard.Sequence; retry.Stop(); Report("Copied — paste with Ctrl+V"); return true; }
            catch (ExternalException) { Report("Clipboard is busy. Try Copy again."); return false; }
            catch (Exception ex) { if (!(ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is InvalidOperationException)) throw; Report("Could not copy this item."); return false; }
        }
        void Report(string message) { if (Feedback != null) Feedback(message); }
        public void Dispose() { retry.Stop(); }
    }
}
