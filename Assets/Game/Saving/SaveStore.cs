using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LordsLegacy.Sim.Save;

namespace LordsLegacy.Game
{
    /// <summary>One save file found by <see cref="SaveStore.List"/>: its path and header (saving.md SV-13, SV-20, SV-34).</summary>
    public sealed class SaveEntry
    {
        /// <summary>Full path of the file.</summary>
        public string Path { get; }
        /// <summary>The header, read without the sections. Null when the save is unreadable.</summary>
        public SaveHeader Header { get; }
        /// <summary>Why this save can't be loaded (SV-34), e.g. <c>Made with an older version of the game</c>. Null when readable.</summary>
        public string UnreadableReason { get; }
        /// <summary>True when the save can be loaded.</summary>
        public bool IsReadable => UnreadableReason == null;
        /// <summary>Name to show: the header name, or the file name without extension for an unreadable save.</summary>
        public string Name { get; }
        /// <summary>Unix seconds used to order the list: the header's real time, or the file's write time when unreadable.</summary>
        public long SortTime { get; }

        /// <summary>Creates an entry for a readable save.</summary>
        public SaveEntry(string path, SaveHeader header)
        {
            Path = path;
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Name = header.Name;
            SortTime = header.CreatedRealTime;
        }

        private SaveEntry(string path, string reason, long sortTime)
        {
            Path = path;
            UnreadableReason = reason ?? throw new ArgumentNullException(nameof(reason));
            Name = System.IO.Path.GetFileNameWithoutExtension(path);
            SortTime = sortTime;
        }

        /// <summary>Creates an entry for a save this build can't read (SV-34).</summary>
        public static SaveEntry Unreadable(string path, string reason, long sortTime) => new SaveEntry(path, reason, sortTime);
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

        /// <summary>Deletes a save file (SV-23). Returns false when the file was already gone.</summary>
        public bool Delete(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        /// <summary>
        /// Every save in the folder, newest first (ties: file path, descending). Readable saves come with their header.
        /// Saves in another format version come back as unreadable entries with a reason (SV-34) so the player can still
        /// delete them. Files that are not saves at all (bad magic, truncated) are skipped. Reads headers only.
        /// </summary>
        public List<SaveEntry> List()
        {
            var result = new List<SaveEntry>();
            if (!Directory.Exists(Folder)) return result;
            foreach (string path in Directory.GetFiles(Folder, "*" + Extension))
            {
                var entry = TryReadEntry(path);
                if (entry != null) result.Add(entry);
            }
            result.Sort((a, b) =>
            {
                int c = b.SortTime.CompareTo(a.SortTime);
                return c != 0 ? c : string.CompareOrdinal(b.Path, a.Path);
            });
            return result;
        }

        /// <summary>Reason shown for a save whose format version differs from this build (SV-33, SV-34).</summary>
        public static string ReasonForVersion(int version) =>
            version < SaveWriter.CurrentVersion ? "Made with an older version of the game" : "Made with a newer version of the game";

        private static SaveEntry TryReadEntry(string path)
        {
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
                if (SaveReader.TryReadFormatVersion(prefix, out int version) && version != SaveWriter.CurrentVersion)
                {
                    long written = new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();
                    return SaveEntry.Unreadable(path, ReasonForVersion(version), written);
                }
                SaveHeader header;
                try { header = SaveReader.ReadHeaderOnly(prefix); }
                catch (SaveFormatException) { header = SaveReader.ReadHeaderOnly(File.ReadAllBytes(path)); }
                return new SaveEntry(path, header);
            }
            catch (Exception e) when (e is SaveFormatException || e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
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
