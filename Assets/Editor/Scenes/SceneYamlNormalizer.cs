#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LordsLegacy.EditorTools.Scenes
{
    /// <summary>
    /// Unity gives new scene objects random fileIDs, so regenerating a scene reorders and renumbers the whole file.
    /// This rewrites a saved text scene with fileIDs derived from (GameObject name, component class, script guid)
    /// so two generator runs give byte-identical files. Objects must have unique names per scene.
    /// </summary>
    public static class SceneYamlNormalizer
    {
        // Scene-level settings documents keep their ids and stay first.
        private static readonly HashSet<int> FixedClasses = new HashSet<int> { 29, 104, 157, 196, 1660057539 };
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(\d+)[^\n]*\n", RegexOptions.Multiline);
        private static readonly Regex PlainRef = new Regex(@"\{fileID: (\d+)\}");
        private static readonly Regex OwnerRef = new Regex(@"m_GameObject: \{fileID: (\d+)\}");
        private static readonly Regex NameLine = new Regex(@"^  m_Name: (.*)$", RegexOptions.Multiline);
        private static readonly Regex ScriptGuid = new Regex(@"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)");

        private sealed class Doc
        {
            public int Class;
            public string Id;
            public string HeaderLine;
            public string Body;
            public string Key;
        }

        /// <summary>Rewrites the scene file in place with deterministic fileIDs and document order.</summary>
        public static void Normalize(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            var matches = Header.Matches(text);
            if (matches.Count == 0) return;
            string preamble = text.Substring(0, matches[0].Index);
            var docs = new List<Doc>();
            for (int i = 0; i < matches.Count; i++)
            {
                int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                docs.Add(new Doc
                {
                    Class = int.Parse(matches[i].Groups[1].Value),
                    Id = matches[i].Groups[2].Value,
                    HeaderLine = matches[i].Value,
                    Body = text.Substring(matches[i].Index + matches[i].Length, end - matches[i].Index - matches[i].Length),
                });
            }

            var goName = new Dictionary<string, string>();
            foreach (var d in docs.Where(d => d.Class == 1))
                goName[d.Id] = NameLine.Match(d.Body).Groups[1].Value;
            foreach (var d in docs.Where(d => !FixedClasses.Contains(d.Class)))
            {
                if (d.Class == 1) { d.Key = goName[d.Id] + "|0"; continue; }
                var owner = OwnerRef.Match(d.Body);
                string name = owner.Success && goName.ContainsKey(owner.Groups[1].Value) ? goName[owner.Groups[1].Value] : "";
                var guid = ScriptGuid.Match(d.Body);
                d.Key = name + "|1|" + d.Class.ToString("D6") + "|" + (guid.Success ? guid.Groups[1].Value : "");
            }

            var movable = docs.Where(d => !FixedClasses.Contains(d.Class))
                .OrderBy(d => d.Key, System.StringComparer.Ordinal).ToList();
            var map = new Dictionary<string, string>();
            for (int i = 0; i < movable.Count; i++) map[movable[i].Id] = (100000 + i).ToString();

            var sb = new StringBuilder(preamble);
            foreach (var d in docs.Where(d => FixedClasses.Contains(d.Class)).Concat(movable))
            {
                string id = map.TryGetValue(d.Id, out var n) ? n : d.Id;
                sb.Append("--- !u!").Append(d.Class).Append(" &").Append(id).Append('\n');
                sb.Append(PlainRef.Replace(d.Body, m => map.TryGetValue(m.Groups[1].Value, out var r) ? "{fileID: " + r + "}" : m.Value));
            }
            File.WriteAllText(path, sb.ToString());
        }
    }
}
#endif
