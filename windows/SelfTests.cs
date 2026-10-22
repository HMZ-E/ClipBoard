using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipBoardWindows
{
    internal static class SelfTests
    {
        static int checks;
        static StringBuilder report = new StringBuilder();
        static void Check(bool pass, string name) { if (!pass) throw new Exception("FAILED: " + name); checks++; report.AppendLine("PASS: " + name); }
        public static int Run(string folder)
        {
            folder = Path.GetFullPath(folder); Directory.CreateDirectory(folder);
            // Isolate rapid atomic-save tests from OneDrive sync locks in the workspace.
            string storageFolder = Path.Combine(Path.GetTempPath(), "ClipBoard-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageFolder);
            string stateFile = Path.Combine(storageFolder, "history.dat");
            try {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var store = new HistoryStore(null);
                string text = "  Exact text\r\nUnicode: مرحبا ✓\t ";
                store.Add(Clip.FromText(text)); Check(store.State.Items[0].Text == text, "Exact Unicode and whitespace preserved");
                Check(!store.Add(Clip.FromText(text)), "Consecutive text copies deduplicated");
                Check(!store.Add(Clip.FromText(" \r\n\t")), "Empty whitespace ignored");
                var pinned = store.State.Items[0]; store.TogglePin(pinned);
                for (int i = 0; i < 65; i++) store.Add(Clip.FromText("item " + i));
                Check(store.State.Items.Count(c => !c.IsPinned) == 50 && store.State.Items.Contains(pinned), "50-entry cap preserves pinned favorites");
                store.TogglePin(pinned); Check(store.State.Items.Count == 50 && !store.State.Items.Contains(pinned), "Unpin reapplies retention cap");
                var removed = store.State.Items[4]; store.Delete(removed); Check(store.CanUndo && !store.State.Items.Contains(removed), "Delete opens Undo window");
                store.Undo(); Check(store.State.Items[4] == removed && !store.CanUndo, "Undo restores original position");
                store.Delete(removed); System.Threading.Thread.Sleep(6100); store.ExpireUndo(); store.Undo();
                Check(!store.State.Items.Contains(removed) && !store.CanUndo, "Undo expires after six seconds");
                store.Add(Clip.FromText("https://www.example.com/path"));
                Check(Presentation.Domain(store.State.Items[0]) == "example.com", "Link domain recognized");
                Check(Presentation.Kind(Clip.FromText("#8cf")) == "Color" && Presentation.Hex(Clip.FromText("#8cf")) == "#88ccff", "Short hex color expanded");
                Check(Presentation.Kind(Clip.FromText("const message = 'hello';")) == "Code", "Code recognized");
                Check(store.Visible("EXAMPLE").Count() == 1, "Search ignores case");
                byte[] png = SamplePng(); var image = Clip.FromImage(png); store.Add(image);
                Check(store.Visible("screenshot").Any(c => c.IsImage) && image.Image(0).PixelWidth == 320, "Image captured and decoded");
                Check(!store.Add(Clip.FromImage(png)), "Consecutive images deduplicated");
                Check(!store.Add(Clip.FromImage(new byte[HistoryStore.MaxImageBytes + 1])), "Oversized images rejected");
                var fake = new FakeClipboard { Value = Clip.FromText("pre-existing"), Sequence = 1 };
                var captureStore = new HistoryStore(null);
                using (var service = new ClipboardService(captureStore, fake)) {
                    service.Capture(); Check(captureStore.State.Items.Count == 0, "Existing clipboard skipped at launch");
                    fake.Value = Clip.FromText("external copy"); fake.Sequence++; service.Changed();
                    Check(captureStore.State.Items.Count == 1, "Clipboard change captured");
                    Check(service.Copy(captureStore.State.Items[0]), "Copy writes through clipboard adapter"); service.Changed();
                    Check(captureStore.State.Items.Count == 1, "Own copy does not create another entry");
                    fake.ReadFailures = 2; fake.Value = Clip.FromText("after contention"); fake.Sequence++;
                    service.Changed(); service.Capture(); service.Capture();
                    Check(captureStore.State.Items[0].Text == "after contention", "Busy clipboard retries without losing the copy");
                    fake.ChangeDuringRead = true; fake.Value = Clip.FromText("stable snapshot"); fake.Sequence++; service.Changed(); service.Capture();
                    Check(captureStore.State.Items[0].Text == "stable snapshot", "Sequence races retry a stable snapshot");
                    captureStore.State.Settings.Paused = true; fake.Value = Clip.FromText("private copy"); fake.Sequence++; service.Changed();
                    captureStore.State.Settings.Paused = false; service.SkipCurrent(); service.Capture();
                    Check(!captureStore.State.Items.Any(c => c.Text == "private copy"), "Paused content is never captured on resume");
                    fake.WriteFails = true; Check(!service.Copy(captureStore.State.Items[0]), "Copy failure reported");
                }
                if (File.Exists(stateFile)) File.Delete(stateFile);
                if (File.Exists(stateFile + ".bak")) File.Delete(stateFile + ".bak");
                var saved = new HistoryStore(stateFile); saved.Add(Clip.FromText(text)); saved.TogglePin(saved.State.Items[0]); saved.Add(Clip.FromImage(png));
                saved.State.Settings.Mood = "Custom"; saved.State.Settings.Color1 = "#112233"; saved.State.Settings.Motion = "Reduced"; saved.UpdateSettings();
                var loaded = new HistoryStore(stateFile);
                Check(loaded.State.Items.Count == 2 && loaded.State.Items.Any(c => c.IsPinned && c.Text == text), "History and pins survive restart");
                Check(loaded.State.Items[0].Png.SequenceEqual(png), "PNG bytes survive encrypted persistence");
                Check(loaded.State.Settings.Color1 == "#112233" && loaded.State.Settings.Mood == "Custom", "Theme preferences survive restart");
                Check(loaded.State.Settings.Motion == "Reduced", "Motion preference survives restart");
                Check(!Encoding.UTF8.GetString(File.ReadAllBytes(stateFile)).Contains("Exact text"), "History file contains encrypted data");
                File.WriteAllText(stateFile, "corrupted primary"); loaded = new HistoryStore(stateFile);
                Check(loaded.State.Items.Count == 2 && loaded.Error != null, "Corrupted primary recovers backup");
                loaded.Clear(); Check(new HistoryStore(stateFile).State.Items.Count == 0 && !File.Exists(stateFile + ".bak"), "Clear removes both history and backup");
                using (var host = Native.CreateHost()) {
                    Check(Native.AddClipboardFormatListener(host.Handle), "Native Windows clipboard listener registers");
                    Check(Native.RemoveClipboardFormatListener(host.Handle), "Native Windows clipboard listener cleans up");
                    bool hotkey = Native.RegisterHotKey(host.Handle, 71, 0x4006, 0x56);
                    report.AppendLine(hotkey ? "PASS: Ctrl+Shift+V hotkey registers" : "NOTE: Ctrl+Shift+V already belongs to another running application");
                    if (hotkey) Native.UnregisterHotKey(host.Handle, 71);
                }
                var demo = new HistoryStore(null); demo.State.Settings.Mood = "Midnight";
                demo.Add(Clip.FromText("const message = \"Hello, clipboard\";")); demo.Add(Clip.FromImage(png));
                demo.Add(Clip.FromText("Small details, smoother days.\nKeep the good stuff one click away."));
                var link = Clip.FromText("https://github.com/HMZ-E/ClipBoard"); demo.Add(link); demo.TogglePin(link);
                var color = Clip.FromText("#80CBC4"); demo.Add(color); demo.TogglePin(color);
                using (var service = new ClipboardService(demo, new FakeClipboard())) {
                    var window = new MainWindow(demo, service);
                    Render(window, Path.Combine(folder, "windows-history.png"));
                    window.Navigate(1); Check(window.SelectedId == link.Id, "Keyboard navigation selects the next pinned item");
                    window.Navigate(1); Check(window.SelectedId == demo.State.Items.First(c => !c.IsPinned).Id, "Keyboard navigation crosses from pins into history");
                    window.ShowPreview(demo.State.Items.First(c => c.IsImage)); Render(window, Path.Combine(folder, "windows-preview.png"));
                    Check(window.PreviewClip.IsImage, "Image preview opens the selected image");
                    window.CloseOverlay(); Check(window.PreviewClip == null, "Closing preview returns to history");
                    demo.State.Settings.Mood = "Custom"; window.ShowSettings(); Render(window, Path.Combine(folder, "windows-settings.png"));
                    window.CloseOverlay(); window.Finish(); Check(File.Exists(Path.Combine(folder, "windows-settings.png")), "History, image preview, and custom settings render");
                }
                TestMotion(folder);
                foreach (string file in new[] { stateFile, stateFile + ".bak", stateFile + ".tmp" }) if (File.Exists(file)) File.Delete(file);
                Directory.Delete(storageFolder, false);
                app.Shutdown(); report.AppendLine("SUCCESS: " + checks + " checks passed. No real clipboard contents were read or changed.");
                File.WriteAllText(Path.Combine(folder, "self-test-results.txt"), report.ToString()); return 0;
            } catch (Exception ex) { report.AppendLine(ex.ToString()); File.WriteAllText(Path.Combine(folder, "self-test-results.txt"), report.ToString()); return 1; }
        }
        static void Pump(int milliseconds)
        {
            var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
        }
        static void TestMotion(string folder)
        {
            Check(new Preferences().Motion == "Full", "Visible full motion is the default, including older settings");
            var store = new HistoryStore(null); store.State.Settings.Mood = "Midnight";
            for (int i = 0; i < 12; i++) store.Add(Clip.FromText("Sample " + i + "\nKeep the good stuff one click away."));
            using (var service = new ClipboardService(store, new FakeClipboard())) {
                var window = new MainWindow(store, service); var panel = (FrameworkElement)window.Content;
                Render(window, Path.Combine(folder, "motion-baseline.png"));
                Motion.Enter(panel, window.MotionSetting, 0, true); Pump(70);
                var parts = Motion.Parts(panel);
                Check(panel.Opacity > .05 && panel.Opacity < .99 && parts.EntryOffset.Y > .1, "Panel entrance advances through visible intermediate frames");
                Render(window, Path.Combine(folder, "motion-enter-early.png"), false);
                Pump(450); Render(window, Path.Combine(folder, "motion-enter-settled.png"), false);
                Check(Math.Abs(panel.Opacity - 1) < .001 && Math.Abs(parts.EntryOffset.Y) < .001 && Math.Abs(parts.EntryScale.ScaleX - 1) < .001, "Panel entrance settles without leaving an offset or scale");
                Check(!File.ReadAllBytes(Path.Combine(folder, "motion-enter-early.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "motion-enter-settled.png"))), "Rendered frames actually change during entrance");
                var clip = store.State.Items[0]; var card = window.CardElement(clip.Id);
                Motion.Enter(card, window.MotionSetting, 180); Pump(60);
                Check(card.Opacity < .01, "Staggered card entrances hold their first frame without flashing");
                Pump(230); Check(card.Opacity > .05 && card.Opacity < .99, "Staggered card entrance starts after its delay");
                Pump(400);
                Motion.Hover(card, window.MotionSetting, true); Pump(80);
                Check(Motion.Parts(card).HoverScale.ScaleX > 1.005, "Hover lift animates the card");
                Motion.Hover(card, window.MotionSetting, false); Pump(220);
                Check(Math.Abs(Motion.Parts(card).HoverScale.ScaleX - 1) < .001, "Hover exit restores card size");
                Check(window.CopyClip(clip), "Copy feedback uses the clipboard adapter"); Pump(80);
                Check(Motion.Parts(card).HoverScale.ScaleX > 1.005, "Copy produces a visible card pulse"); Pump(400);
                window.SmoothScrollTo(150); Pump(75);
                Check(window.CurrentScrollOffset > 0 && window.CurrentScrollOffset < 150, "Wheel scrolling moves through intermediate offsets");
                Pump(250); Check(Math.Abs(window.CurrentScrollOffset - 150) < 2, "Smooth scrolling reaches its target");
                window.ShowPreview(clip); Pump(75); var preview = window.OverlayElement;
                Check(preview.Opacity > .05 && preview.Opacity < .99 && Motion.Parts(preview).EntryScale.ScaleX < 1, "Preview opens with fade and zoom");
                Pump(400); window.CloseOverlay(); Pump(60);
                Check(preview.Parent != null && preview.Opacity > 0 && preview.Opacity < 1, "Preview stays attached while its closing fade runs");
                Pump(200); Check(preview.Parent == null, "Preview is removed after its closing animation");
                var brush = (LinearGradientBrush)((Border)panel).Background; Color oldColor = brush.GradientStops[0].Color;
                store.State.Settings.Mood = "Sunset"; window.ApplyMood(); Pump(80);
                Color middle = brush.GradientStops[0].Color; Pump(700); Color last = brush.GradientStops[0].Color;
                Check(middle != oldColor && middle != last, "Theme colors interpolate instead of switching instantly");
                window.SetBackdropRunning(true); Check(window.BackdropRunning, "Moving background starts in Full mode");
                Render(window, Path.Combine(folder, "motion-background-a.png"), false); Pump(400); Render(window, Path.Combine(folder, "motion-background-b.png"), false);
                Check(!File.ReadAllBytes(Path.Combine(folder, "motion-background-a.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "motion-background-b.png"))), "Background glow produces changing rendered frames");
                window.SetBackdropRunning(false); Check(!window.BackdropRunning, "Background animation stops when the panel is hidden");
                store.State.Settings.Motion = "Reduced"; window.SetBackdropRunning(true);
                Check(!window.BackdropRunning, "Reduced mode disables continuous background motion");
                window.ShowPreview(clip); Pump(70);
                Check(window.OverlayElement.Opacity < 1 && Math.Abs(Motion.Parts(window.OverlayElement).EntryScale.ScaleX - 1) < .001, "Reduced mode uses fades without zoom");
                window.CloseOverlay(); Pump(200); store.State.Settings.Motion = "Off";
                window.ShowPreview(clip); Check(window.OverlayElement.Opacity == 1 && Motion.Parts(window.OverlayElement).EntryOffset.Y == 0, "Off mode opens without motion");
                window.CloseOverlay(); window.Finish();
            }
        }
        static void Render(MainWindow window, string path, bool settle = true)
        {
            var content = (FrameworkElement)window.Content; content.Measure(new Size(410, 650)); content.Arrange(new Rect(0, 0, 410, 650)); content.UpdateLayout();
            if (settle) Pump(500);
            var bitmap = new RenderTargetBitmap(820, 1300, 192, 192, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) encoder.Save(file);
        }
        static byte[] SamplePng()
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                dc.DrawRectangle(new LinearGradientBrush(MainWindow.Brush("#164149").Color, MainWindow.Brush("#101C2B").Color, 45), null, new Rect(0, 0, 320, 140));
                var typeface = new Typeface("Segoe UI");
                dc.DrawText(new FormattedText("CLIPBOARD / WINDOWS", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, MainWindow.Brush("#80CBC4"), 1.0), new Point(24, 22));
                dc.DrawText(new FormattedText("Keep the good stuff close.", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 20, Brushes.White, 1.0), new Point(24, 47));
                dc.DrawText(new FormattedText("Text. Images. Favorites. One shortcut.", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, MainWindow.Brush("#D1DADF"), 1.0), new Point(24, 87));
            }
            var bitmap = new RenderTargetBitmap(320, 140, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = new MemoryStream()) { encoder.Save(stream); return stream.ToArray(); }
        }
        sealed class FakeClipboard : IClipboardAccess
        {
            public uint Sequence { get; set; }
            public Clip Value;
            public int ReadFailures;
            public bool WriteFails, ChangeDuringRead;
            public Clip Read() { if (ReadFailures-- > 0) throw new ExternalException(); if (ChangeDuringRead) { Sequence++; ChangeDuringRead = false; } return Value; }
            public void Write(Clip clip) { if (WriteFails) throw new ExternalException(); Value = clip; Sequence++; }
        }
    }
}
