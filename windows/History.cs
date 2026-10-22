using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace ClipBoardWindows
{
    public sealed class Clip
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public byte[] Png { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPinned { get; set; }
        [ScriptIgnore] public bool IsImage { get { return Png != null; } }
        [ScriptIgnore] public string Kind { get { return Presentation.Kind(this); } }
        [ScriptIgnore] public long Size { get { return IsImage ? Png.LongLength : (Text ?? "").Length * 2L; } }
        public static Clip FromText(string text) { return new Clip { Id = Guid.NewGuid().ToString(), Text = text, CreatedAt = DateTime.UtcNow }; }
        public static Clip FromImage(byte[] png) { return new Clip { Id = Guid.NewGuid().ToString(), Png = png, CreatedAt = DateTime.UtcNow }; }
        public BitmapSource Image(int decodeWidth)
        {
            using (var stream = new MemoryStream(Png)) {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) bitmap.DecodePixelWidth = decodeWidth;
                bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
            }
        }
    }
    public static class Presentation
    {
        public static string Hex(Clip clip)
        {
            string text = (clip.Text ?? "").Trim();
            if (!Regex.IsMatch(text, @"^(#[0-9a-fA-F]{3}|#?[0-9a-fA-F]{6})$")) return null;
            text = text.TrimStart('#');
            return "#" + (text.Length == 3 ? string.Concat(text.Select(c => new string(c, 2))) : text);
        }
        public static string Domain(Clip clip)
        {
            Uri url; string text = (clip.Text ?? "").Trim();
            if (text.Any(char.IsWhiteSpace) || !Uri.TryCreate(text, UriKind.Absolute, out url) ||
                (url.Scheme != "https" && url.Scheme != "http") || string.IsNullOrEmpty(url.Host)) return null;
            return url.Host.StartsWith("www.") ? url.Host.Substring(4) : url.Host;
        }
        public static string Kind(Clip clip)
        {
            if (clip.IsImage) return "Image";
            if (Hex(clip) != null) return "Color";
            if (Domain(clip) != null) return "Link";
            if ((clip.Text ?? "").TrimStart().StartsWith("```") || Regex.IsMatch(clip.Text ?? "", @"(?m)^\s*(import [A-Za-z_]|from [A-Za-z_].* import |(let|var|const) \w+\s*[:=]|func \w+\(|def \w+\(|function[ (]|(struct|class|enum) \w+.*\{|SELECT .* FROM )")) return "Code";
            return "Text";
        }
    }
    public sealed class Preferences
    {
        public string Mood { get; set; }
        public string Intensity { get; set; }
        public string Motion { get; set; }
        public string Color1 { get; set; }
        public string Color2 { get; set; }
        public string Color3 { get; set; }
        public bool Paused { get; set; }
        public Preferences() { Mood = "Chrome"; Intensity = "Balanced"; Motion = "Full"; Color1 = "#8080FF"; Color2 = "#FF80BF"; Color3 = "#182038"; }
    }
    public sealed class SavedState
    {
        public int Version { get; set; }
        public List<Clip> Items { get; set; }
        public Preferences Settings { get; set; }
        public SavedState() { Version = 1; Items = new List<Clip>(); Settings = new Preferences(); }
    }
    public sealed class HistoryStore
    {
        public const int MaxHistory = 50;
        public const long MaxBytes = 64L * 1024 * 1024;
        public const int MaxImageBytes = 8 * 1024 * 1024;
        public SavedState State { get; private set; }
        public string Error { get; private set; }
        public Clip Deleted { get; private set; }
        public DateTime UndoUntil { get; private set; }
        int deletedIndex;
        readonly string path;
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 256 * 1024 * 1024, RecursionLimit = 100 };
        public event Action Changed;
        public HistoryStore(string file) { path = file; State = new SavedState(); if (file != null) Load(); }
        public IEnumerable<Clip> Visible(string search)
        {
            return State.Items.Where(c => (c.IsImage ? "image screenshot" : c.Text ?? "").IndexOf(search ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.CreatedAt);
        }
        public bool Add(Clip clip)
        {
            if (clip == null || (!clip.IsImage && string.IsNullOrWhiteSpace(clip.Text))) return false;
            if (clip.Size > (clip.IsImage ? MaxImageBytes : 2 * 1024 * 1024)) { Error = "Copy is too large to save in history."; Notify(); return false; }
            Clip first = State.Items.FirstOrDefault();
            if (first != null && first.IsImage == clip.IsImage && (clip.IsImage ? first.Png.SequenceEqual(clip.Png) : first.Text == clip.Text)) return false;
            if (State.Items.Where(c => c.IsPinned).Sum(c => c.Size) + clip.Size > MaxBytes) { Error = "Pinned items fill the history storage limit. Remove a pin to make room."; Notify(); return false; }
            State.Items.Insert(0, clip); Trim(); Save(); Notify(); return true;
        }
        void Trim()
        {
            while (State.Items.Count(c => !c.IsPinned) > MaxHistory || State.Items.Sum(c => c.Size) > MaxBytes) {
                int i = State.Items.FindLastIndex(c => !c.IsPinned); if (i < 0) break; State.Items.RemoveAt(i);
            }
        }
        public void TogglePin(Clip clip) { clip.IsPinned = !clip.IsPinned; Trim(); Save(); Notify(); }
        public void Delete(Clip clip)
        {
            int i = State.Items.IndexOf(clip); if (i < 0) return;
            Deleted = clip; deletedIndex = i; UndoUntil = DateTime.UtcNow.AddSeconds(6);
            State.Items.RemoveAt(i); Save(); Notify();
        }
        public bool CanUndo { get { return Deleted != null && DateTime.UtcNow < UndoUntil; } }
        public void Undo()
        {
            if (!CanUndo) { Deleted = null; return; }
            State.Items.Insert(Math.Min(deletedIndex, State.Items.Count), Deleted); Deleted = null; Trim(); Save(); Notify();
        }
        public void ExpireUndo() { if (Deleted != null && !CanUndo) { Deleted = null; Notify(); } }
        public void Clear()
        {
            State.Items.Clear(); Deleted = null;
            Save();
            // Remove the previous encrypted snapshot too, so Clear does not leave a recoverable backup.
            if (path != null && File.Exists(path + ".bak")) { try { File.Delete(path + ".bak"); } catch (IOException) { Error = "History cleared; the backup could not be removed."; } }
            Notify();
        }
        public void UpdateSettings() { Save(); Notify(); }
        void Notify() { if (Changed != null) Changed(); }
        public void Save()
        {
            if (path == null) return;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(json.Serialize(State)), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(path + ".tmp", data);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
                Error = null;
            } catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException)) throw; Error = "History could not be saved. Check the local data folder permissions."; }
        }
        void Load()
        {
            if (!File.Exists(path)) return;
            foreach (string file in new[] { path, path + ".bak" }) {
                try {
                    if (new FileInfo(file).Length > 256L * 1024 * 1024) throw new InvalidDataException();
                    byte[] bytes = ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser);
                    var state = json.Deserialize<SavedState>(Encoding.UTF8.GetString(bytes));
                    if (state == null || state.Version != 1 || state.Items == null || state.Settings == null) throw new InvalidDataException();
                    state.Items = state.Items.Where(c => c != null && !string.IsNullOrEmpty(c.Id) && (c.IsImage || c.Text != null)).ToList();
                    State = state; Trim(); if (file != path) Error = "Recovered the previous history backup."; return;
                } catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException || ex is ArgumentException || ex is InvalidOperationException)) throw; }
            }
            Error = "Saved history could not be read. A new history will start with your next copy.";
        }
    }
}
