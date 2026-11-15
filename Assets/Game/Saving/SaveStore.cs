using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LordsLegacy.Sim.Save;

namespace LordsLegacy.Game
{
    /// <summary>One save file found by <see cref="SaveStore.List"/>: its path and header (saving.md SV-13, SV-20).</summary>
    public sealed class SaveEntry
    {
        /// <summary>Full path of the file.</summary>
        public string Path { get; }
        /// <summary>The header, read without the sections. Null when <see cref="UnreadableReason"/> is set.</summary>
        public SaveHeader Header { get; }
        /// <summary>Why this build can't read the file (SV-34), or null when it is readable.</summary>
        public string UnreadableReason { get; }
        /// <summary>Display name: the header name, or the file name for an unreadable save.</summary>
        public string Name => Header != null ? Header.Name : System.IO.Path.GetFileNameWithoutExtension(Path);
        /// <summary>Real creation time (unix seconds): from the header, or the file's last write time when unreadable.</summary>
        public long CreatedRealTime { get; }
        /// <summary>True when the header was read.</summary>
        public bool IsReadable => Header != null;

        /// <summary>Creates a readable entry.</summary>
        public SaveEntry(string path, SaveHeader header)
        {
            Path = path; Header = header; CreatedRealTime = header.CreatedRealTime;
        }

        /// <summary>Creates an entry for a file this build can't read.</summary>
        public SaveEntry(string path, string unreadableReason, long createdRealTime)
        {
            Path = path; UnreadableReason = unreadableReason; CreatedRealTime = createdRealTime;
        }
    }

    /// <summary>
    /// Save files in one folder (normally <c>persistentDataPath/Saves/</c>). Manual saves always get a new unique file name
    /// and never overwrite another save (SV-10); the autosave slot is the only file that is overwritten (SV-11).
    /// </summary>
    public sealed class SaveStore
    {
        /// <summary>File extension of save files.</summary>
        public const string Extension = ".llsave";
        /// <summary>File name stem of the single autosave slot.</summary>
        public const string AutosaveStem = "Autosave";

        private const int HeaderPrefixBytes = 4096;

        /// <summary>The folder the saves live in.</summary>
        public string Folder { get; }

        /// <summary>Creates a store over a folder. The folder is created on first write.</summary>
        public SaveStore(string folder)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        }

        /// <summary>The store for the real game: <c>Application.persistentDataPath/Saves</c>.</summary>
        public static SaveStore Default() =>
            new SaveStore(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"));

        /// <summary>Writes a new manual save under a file name not used by any other file and returns its path.</summary>
        public string WriteNew(string name, byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            Directory.CreateDirectory(Folder);
            string stem = SafeStem(name);
            if (string.Equals(stem, AutosaveStem, StringComparison.OrdinalIgnoreCase)) stem = AutosaveStem + "_manual";
            for (int n = 0; ; n++)
            {
                string path = System.IO.Path.Combine(Folder, n == 0 ? stem + Extension : stem + "_" + (n + 1) + Extension);
                if (File.Exists(path)) continue;
                try
                {
                    using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                        fs.Write(bytes, 0, bytes.Length);
                    return path;
                }
                catch (IOException) when (File.Exists(path)) { /* taken meanwhile: try the next suffix */ }
            }
        }

        /// <summary>Writes (overwriting) the single autosave slot and returns its path.</summary>
        public string WriteAutosave(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            Directory.CreateDirectory(Folder);
            string path = System.IO.Path.Combine(Folder, AutosaveStem + Extension);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <summary>Reads the bytes of a save file.</summary>
        public byte[] ReadAll(string path) => File.ReadAllBytes(path);

        /// <summary>Reason shown for a save written by another format version (SV-33, SV-34).</summary>
        public const string OlderVersionReason = "Made with an older version of the game";

        /// <summary>Deletes a save file (SV-23). Returns true when the file is gone afterwards. The autosave may be deleted too.</summary>
        public bool Delete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return false; }
        }

        /// <summary>
        /// Every save file, newest first, including ones this build can't read (those carry an
        /// <see cref="SaveEntry.UnreadableReason"/>, SV-34). Files that can't be opened at all are skipped.
        /// </summary>
        public List<SaveEntry> ListAll()
        {
            var result = new List<SaveEntry>();
            if (!Directory.Exists(Folder)) return result;
            foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
            {
                try
                {
                    var header = TryReadHeader(path, out string reason);
                    if (header != null) result.Add(new SaveEntry(path, header));
                    else if (reason != null)
                        result.Add(new SaveEntry(path, reason, new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds()));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            }
            result.Sort((a, b) =>
            {
                int c = b.CreatedRealTime.CompareTo(a.CreatedRealTime);
                return c != 0 ? c : string.CompareOrdinal(b.Path, a.Path);
            });
            return result;
        }

        /// <summary>Every readable save, newest first by real creation time (ties: file path, descending). Reads headers only.</summary>
        public List<SaveEntry> List()
        {
            var result = new List<SaveEntry>();
            if (!Directory.Exists(Folder)) return result;
            foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
            {
                var header = TryReadHeader(path);
                if (header != null) result.Add(new SaveEntry(path, header));
            }
            result.Sort((a, b) =>
            {
                int c = b.Header.CreatedRealTime.CompareTo(a.Header.CreatedRealTime);
                return c != 0 ? c : string.CompareOrdinal(b.Path, a.Path);
            });
            return result;
        }

        private static SaveHeader TryReadHeader(string path) => TryReadHeader(path, out _);

        private static SaveHeader TryReadHeader(string path, out string unreadableReason)
        {
            unreadableReason = null;
            try
            {
                byte[] prefix;
                using (var fs = File.OpenRead(path))
                {
                    int len = (int)Math.Min(fs.Length, HeaderPrefixBytes);
                    prefix = new byte[len];
                    int got = 0;
                    while (got < len)
                    {
                        int r = fs.Read(prefix, got, len - got);
                        if (r <= 0) break;
                        got += r;
                    }
                }
                try { return SaveReader.ReadHeaderOnly(prefix); }
                catch (SaveFormatException) { return SaveReader.ReadHeaderOnly(File.ReadAllBytes(path)); }
            }
            catch (SaveFormatException e)
            {
                unreadableReason = IsOlderVersion(path) ? OlderVersionReason : "Can't be read: " + e.Message;
                return null;
            }
            catch (SaveMapMismatchException)
            {
                unreadableReason = "Made on a different map";
                return null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsOlderVersion(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var b = new byte[8];
                    if (fs.Read(b, 0, 8) < 8) return false;
                    if (b[0] != (byte)'L' || b[1] != (byte)'L' || b[2] != (byte)'S' || b[3] != (byte)'V') return false;
                    return BitConverter.ToInt32(b, 4) != SaveWriter.CurrentVersion;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return false; }
        }

        private static string SafeStem(string name)
        {
            var sb = new StringBuilder();
            var bad = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in name ?? "")
                sb.Append(Array.IndexOf(bad, c) >= 0 ? '-' : c);
            string s = sb.ToString().Trim().TrimEnd('.');
            if (s.Length > 80) s = s.Substring(0, 80).TrimEnd();
            return s.Length == 0 ? "Save" : s;
        }
    }
}
