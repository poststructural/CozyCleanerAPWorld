using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CozyCleanerAP
{
    // NOTE: the game ships a heavily stripped Newtonsoft.Json (no DeserializeObject), so all structured
    // JSON goes through UnityEngine.JsonUtility (used by the game itself, therefore never stripped).
    [Serializable]
    public class LevelInfo
    {
        public int id;
        public string scene = "";
        public int build_index;
        public string kind = "";
        public string name = "";
        public string verb = "";
        public int total;
        public int stage1;
        public int stage2;
        public List<string> tools = new List<string>();
        public bool HasStages { get { return kind == "tutorial" || kind == "house"; } }
    }

    [Serializable]
    public class SlotData
    {
        public string goal = "final_level";
        public int levels_needed = 25;
        public bool toolsanity;
        public bool shopsanity;
        public int money_multiplier = 1;
        public bool death_link;
        public string seed = "";
        public string player = "";
        public List<LevelInfo> levels = new List<LevelInfo>();
    }

    [Serializable] class ItemLine { public int index = -1; public string item = ""; public string player = ""; }
    [Serializable] class StringList { public List<string> v = new List<string>(); }
    [Serializable]
    class StateDto
    {
        public int applied = -1;
        public bool goal_sent;
        public bool goal_pending;
        public List<string> sent = new List<string>();
        public List<string> outbox = new List<string>();
        public List<string> purchase_keys = new List<string>();
        public List<int> purchase_vals = new List<int>();
    }
    [Serializable]
    class PrefsDto { public List<string> keys = new List<string>(); public List<int> vals = new List<int>(); }

    public class ItemRec
    {
        public int Index;
        public string Name;
        public string Player;
    }

    public class PluginState
    {
        public int applied = -1;
        public Dictionary<string, int> purchases = new Dictionary<string, int>();
        public HashSet<string> sent = new HashSet<string>();
        public List<string> outbox = new List<string>();   // checks not yet durably appended to checks.jsonl
        public bool goal_sent;
        public bool goal_pending;
    }

    /// <summary>File bridge between the Python client and this plugin. All calls are main-thread.</summary>
    public static class Bridge
    {
        public static string Root;
        public static string Dir;            // active bridge folder (may be null before the client ran)
        public static SlotData Slot;         // null = not connected (vanilla behaviour)
        public static PluginState State = new PluginState();
        public static HashSet<string> Checked = new HashSet<string>();
        public static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public static readonly List<ItemRec> Received = new List<ItemRec>();
        public static readonly Queue<ItemRec> Pending = new Queue<ItemRec>();
        public static Dictionary<string, int> Prefs = new Dictionary<string, int>();

        static readonly HashSet<int> seenIdx = new HashSet<int>();
        static long itemOffset;
        static string slotStamp = "", checkedStamp = "";
        static bool stateDirty, prefsDirty;
        static string lastStatus = "";
        public static string FrozenSaveDir;  // chosen the first time SaveManager asks (cannot change mid-run)
        public static bool SaveManagerBooted, SaveIsolated;
        public static string MismatchSeed;   // current.txt moved to another seed after the save paths were frozen

        /// <summary>Slot data is known but AP behaviour must stay off until the game is restarted.</summary>
        public static bool Blocked
        {
            get { return Slot != null && ((SaveManagerBooted && !SaveIsolated) || MismatchSeed != null); }
        }
        public static string BlockedSeed
        {
            get { return MismatchSeed ?? (Slot != null ? Slot.seed : ""); }
        }
        /// <summary>Folder holding the isolated AP save (also used for cloud_levels.json), or null when vanilla.</summary>
        public static string ActiveSaveDir
        {
            get
            {
                if (FrozenSaveDir != null) return SaveIsolated ? FrozenSaveDir : null;
                return Connected ? SaveDir : null;
            }
        }

        public static bool Connected { get { return Slot != null && !Blocked; } }
        public static string SaveDir { get { return Dir == null ? null : Path.Combine(Dir, "save"); } }
        public static string CurrentTxt { get { return Path.Combine(Root, "current.txt"); } }

        // ---------------------------------------------------------------- init
        public static void Init()
        {
            string home = null;
            try { home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch { }
            if (string.IsNullOrEmpty(home))
                home = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetEnvironmentVariable("USERPROFILE") ?? ".";
            Root = Path.Combine(home, "CozyCleanerAP");
            try { Directory.CreateDirectory(Root); } catch { }
            string cur = ReadCurrent();
            if (cur != null) SwitchDir(Path.Combine(Root, cur));
            else Plugin.Log.LogWarning("[CozyAP] current.txt not found in " + Root + " - start the Archipelago client before the game. Running vanilla.");
        }

        static string ReadCurrent()
        {
            try
            {
                string t = ReadShared(CurrentTxt);
                if (t == null) return null;
                t = t.Trim();
                return t.Length == 0 ? null : t;
            }
            catch { return null; }
        }

        static void SwitchDir(string dir)
        {
            Dir = dir;
            Slot = null;
            State = new PluginState();
            Checked = new HashSet<string>();
            Counts.Clear(); Received.Clear(); Pending.Clear(); seenIdx.Clear();
            itemOffset = 0; slotStamp = ""; checkedStamp = "";
            Prefs = new Dictionary<string, int>();
            stateDirty = prefsDirty = false; lastStatus = "";
            try { Directory.CreateDirectory(Dir); } catch { }
            LoadState();
            LoadPrefs();
            ReloadSlot();
            ReloadChecked();
            ReadItems();
            Plugin.Log.LogInfo("[CozyAP] bridge=" + Dir + " connected=" + Connected + " items=" + Received.Count + " sent=" + State.sent.Count);
        }

        static string P(string f) { return Path.Combine(Dir, f); }

        // ---------------------------------------------------------------- polling
        public static void Poll()
        {
            try
            {
                string cur = ReadCurrent();
                if (cur != null)
                {
                    string want = Path.Combine(Root, cur);
                    if (Dir != null && FrozenSaveDir != null && SaveIsolated
                        && !string.Equals(want, Dir, StringComparison.Ordinal))
                    {
                        // save paths are frozen to the old seed: never mix in the new one
                        if (MismatchSeed != cur)
                        {
                            MismatchSeed = cur;
                            Plugin.Log.LogWarning("[CozyAP] current.txt now points to " + cur + " but this run is bound to " + Dir + ". Restart the game.");
                        }
                    }
                    else if (Dir == null || !string.Equals(want, Dir, StringComparison.Ordinal))
                    {
                        bool bootedVanilla = SaveManagerBooted && !SaveIsolated;
                        MismatchSeed = null;
                        SwitchDir(want);
                        Toasts.Add("AP: bridge folder changed -> " + cur);
                        if (Connected && FrozenSaveDir != null && !string.Equals(Path.Combine(Dir, "save"), FrozenSaveDir))
                            Plugin.Log.LogWarning("[CozyAP] Bridge changed while the game runs; the save folder stays " + FrozenSaveDir + ". Restart the game.");
                        if (Connected && bootedVanilla)
                            Plugin.Log.LogWarning("[CozyAP] SaveManager already loaded the normal save. Restart the game after starting the client.");
                    }
                    else if (MismatchSeed != null) MismatchSeed = null;
                }
                if (Dir == null) return;
                bool wasSlot = Slot != null;
                ReloadSlot();
                if (!wasSlot && Slot != null)
                {
                    if (Blocked)
                    {
                        Plugin.Log.LogWarning("[CozyAP] Connected after the game loaded its normal save - AP behaviour disabled. Restart the game.");
                        Toasts.Add("Archipelago: restart the game to play seed " + Slot.seed);
                    }
                    else Toasts.Add("AP: connected to " + Slot.seed);
                }
                ReloadChecked();
                ReadItems();
                FlushOutbox();
                FlushDirty();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] Poll: " + e.Message); }
        }

        static void ReloadSlot()
        {
            try
            {
                string f = P("slot.json");
                if (!File.Exists(f)) { return; }
                string txt = ReadShared(f);
                if (string.IsNullOrEmpty(txt)) return;
                if (txt == slotStamp && Slot != null) return;
                string t = txt;
                var s = JsonUtility.FromJson<SlotData>(txt);
                if (s == null) return;
                if (s.levels == null) s.levels = new List<LevelInfo>();
                if (s.levels.Count == 0) s.levels = ParseLevels(txt);
                Plugin.Log.LogInfo("[CozyAP] slot.json: " + s.levels.Count + " levels");
                Slot = s;
                slotStamp = t;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] slot.json: " + e.Message); }
        }

        // The stripped engine's JsonUtility can drop a list of nested objects; parse each level object on its own.
        static List<LevelInfo> ParseLevels(string txt)
        {
            var list = new List<LevelInfo>();
            int at = txt.IndexOf("\"levels\"");
            if (at < 0) return list;
            int open = txt.IndexOf('[', at);
            if (open < 0) return list;
            // level objects hold no nested objects, so each runs from '{' to the next '}'
            for (int i = txt.IndexOf('{', open); i >= 0; i = txt.IndexOf('{', i + 1))
            {
                int end = txt.IndexOf('}', i);
                if (end < 0) break;
                try { var l = JsonUtility.FromJson<LevelInfo>(txt.Substring(i, end - i + 1)); if (l != null && l.id > 0) list.Add(l); }
                catch { }
                i = end;
            }
            return list;
        }

        static void ReloadChecked()
        {
            try
            {
                string f = P("checked.json");
                if (!File.Exists(f)) return;
                string txt = ReadShared(f);
                if (string.IsNullOrWhiteSpace(txt)) return;
                if (txt == checkedStamp) return;
                string t = txt;
                var list = JsonUtility.FromJson<StringList>("{\"v\":" + txt.Trim() + "}");
                if (list != null && list.v != null) { Checked = new HashSet<string>(list.v); checkedStamp = t; }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] checked.json: " + e.Message); }
        }

        static void ReadItems()
        {
            try
            {
                string f = P("items.jsonl");
                if (!File.Exists(f)) return;
                byte[] buf;
                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length < itemOffset) itemOffset = 0;
                    if (fs.Length == itemOffset) return;
                    fs.Seek(itemOffset, SeekOrigin.Begin);
                    buf = new byte[fs.Length - itemOffset];
                    int n = 0;
                    while (n < buf.Length) { int r = fs.Read(buf, n, buf.Length - n); if (r <= 0) break; n += r; }
                    if (n < buf.Length) Array.Resize(ref buf, n);
                }
                int last = Array.LastIndexOf(buf, (byte)'\n');
                if (last < 0) return; // partial line, wait for more
                string text = Encoding.UTF8.GetString(buf, 0, last + 1);
                itemOffset += last + 1;
                foreach (string raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    try
                    {
                        var o = JsonUtility.FromJson<ItemLine>(line);
                        if (o == null) continue;
                        var rec = new ItemRec { Index = o.index, Name = o.item, Player = o.player ?? "" };
                        if (string.IsNullOrEmpty(rec.Name) || rec.Index < 0) continue;
                        if (!seenIdx.Add(rec.Index)) continue;
                        Received.Add(rec);
                        int c; Counts.TryGetValue(rec.Name, out c); Counts[rec.Name] = c + 1;
                        if (rec.Index > State.applied) Pending.Enqueue(rec);
                    }
                    catch { }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] items.jsonl: " + e.Message); }
        }

        public static bool HasItem(string name) { int c; return Counts.TryGetValue(name, out c) && c > 0; }
        public static int Count(string name) { int c; Counts.TryGetValue(name, out c); return c; }

        // ---------------------------------------------------------------- level table
        public static LevelInfo LevelById(int id)
        {
            if (Slot == null) return null;
            foreach (var l in Slot.levels) if (l.id == id) return l;
            return null;
        }
        public static LevelInfo LevelByScene(string scene)
        {
            if (Slot == null || scene == null) return null;
            foreach (var l in Slot.levels) if (l.scene == scene) return l;
            return null;
        }
        public static LevelInfo LevelByBuild(int idx)
        {
            if (Slot == null) return null;
            foreach (var l in Slot.levels) if (l.build_index == idx) return l;
            return null;
        }
        public static LevelInfo CurrentLevel()
        {
            if (Slot == null) return null;
            LevelInfo l = null;
            try
            {
                if (level_controller.Instance != null) l = LevelById(level_controller.Instance.currentLevelID);
                if (l == null) l = LevelByScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            catch { }
            return l;
        }

        // ---------------------------------------------------------------- checks
        public static bool Has(string loc) { return State.sent.Contains(loc) || Checked.Contains(loc) || State.outbox.Contains(loc); }

        public static void Send(string loc, bool quiet = false)
        {
            SendMany(new[] { loc }, quiet);
        }

        public static void SendMany(IEnumerable<string> locs, bool quiet = false)
        {
            if (!Connected || Dir == null) return;
            var fresh = new List<string>();
            foreach (var loc in locs)
            {
                if (string.IsNullOrEmpty(loc) || Has(loc)) continue;
                State.outbox.Add(loc);
                fresh.Add(loc);
            }
            if (fresh.Count == 0) return;
            stateDirty = true;
            if (!quiet)
                foreach (var l in fresh) Toasts.Add("Check: " + l);
            else Toasts.Add("Checks sent: " + fresh.Count);
            FlushOutbox();
            FlushDirty();   // persist the outbox immediately so a crash cannot lose a check
        }

        public static void SendGoal()
        {
            if (!Connected || State.goal_sent || State.goal_pending) return;
            State.goal_pending = true;
            stateDirty = true;
            Toasts.Add("Goal complete!");
            FlushOutbox();
            FlushDirty();
        }

        /// <summary>Append queued checks to checks.jsonl; they only move to State.sent after a successful write.</summary>
        public static void FlushOutbox()
        {
            if (!Connected || Dir == null) return;
            if (State.outbox.Count == 0 && !State.goal_pending) return;
            var sb = new StringBuilder();
            foreach (var loc in State.outbox) sb.Append("{\"location\": \"").Append(Esc(loc)).Append("\"}\n");
            if (State.goal_pending) sb.Append("{\"type\": \"goal\"}\n");
            if (!Append(P("checks.jsonl"), sb.ToString())) return;
            foreach (var loc in State.outbox) State.sent.Add(loc);
            State.outbox.Clear();
            if (State.goal_pending) { State.goal_sent = true; State.goal_pending = false; }
            stateDirty = true;
        }

        public static int PurchaseCount(string tool) { int c; State.purchases.TryGetValue(tool, out c); return c; }
        public static void SetPurchaseCount(string tool, int n) { State.purchases[tool] = n; stateDirty = true; }
        public static void MarkApplied(int idx) { if (idx > State.applied) { State.applied = idx; stateDirty = true; } }

        // ---------------------------------------------------------------- state files
        static void LoadState()
        {
            try
            {
                string txt = File.Exists(P("plugin_state.json")) ? ReadShared(P("plugin_state.json")) : null;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    var d = JsonUtility.FromJson<StateDto>(txt);
                    if (d != null)
                    {
                        var s = new PluginState { applied = d.applied, goal_sent = d.goal_sent, goal_pending = d.goal_pending };
                        if (d.sent != null) s.sent = new HashSet<string>(d.sent);
                        if (d.outbox != null) s.outbox = new List<string>(d.outbox);
                        if (d.purchase_keys != null && d.purchase_vals != null)
                            for (int i = 0; i < d.purchase_keys.Count && i < d.purchase_vals.Count; i++) s.purchases[d.purchase_keys[i]] = d.purchase_vals[i];
                        State = s;
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] plugin_state.json: " + e.Message); }
        }

        static void LoadPrefs()
        {
            try
            {
                string txt = File.Exists(P("prefs.json")) ? ReadShared(P("prefs.json")) : null;
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    var d = JsonUtility.FromJson<PrefsDto>(txt);
                    if (d != null && d.keys != null && d.vals != null)
                        for (int i = 0; i < d.keys.Count && i < d.vals.Count; i++) Prefs[d.keys[i]] = d.vals[i];
                }
            }
            catch { }
        }

        public static void PrefsDirty() { prefsDirty = true; }

        public static void FlushDirty()
        {
            if (Dir == null) return;
            try
            {
                if (stateDirty)
                {
                    var dto = new StateDto { applied = State.applied, goal_sent = State.goal_sent, goal_pending = State.goal_pending, sent = State.sent.ToList(), outbox = State.outbox.ToList() };
                    foreach (var kv in State.purchases) { dto.purchase_keys.Add(kv.Key); dto.purchase_vals.Add(kv.Value); }
                    WriteAtomic(P("plugin_state.json"), JsonUtility.ToJson(dto, true));
                    stateDirty = false;
                }
                if (prefsDirty)
                {
                    var pd = new PrefsDto();
                    foreach (var kv in Prefs) { pd.keys.Add(kv.Key); pd.vals.Add(kv.Value); }
                    WriteAtomic(P("prefs.json"), JsonUtility.ToJson(pd, true));
                    prefsDirty = false;
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] flush: " + e.Message); }
        }

        public static void WriteStatus()
        {
            if (Dir == null) return;
            try
            {
                var lv = CurrentLevel();
                string s = "{\"level\": " + (lv != null ? lv.id : 0)
                    + ", \"scene\": \"" + Esc(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                    + "\", \"connected_seed\": \"" + Esc(Connected ? Slot.seed : "") + "\"}";
                if (s == lastStatus) return;
                lastStatus = s;
                WriteAtomic(P("status.json"), s);
            }
            catch { }
        }

        // ---------------------------------------------------------------- io helpers
        public static string ReadShared(string path)
        {
            if (!File.Exists(path)) return null;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        static bool Append(string path, string text)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] b = new UTF8Encoding(false).GetBytes(text);
                    fs.Write(b, 0, b.Length);
                    fs.Flush(true);
                }
                return true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] append " + Path.GetFileName(path) + ": " + e.Message); return false; }
        }

        static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            try
            {
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch
            {
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
        }

        static string Esc(string v)
        {
            var sb = new StringBuilder();
            foreach (char c in v ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- naming helpers
        public static string AchievementTitle(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            string s = id;
            if (s.StartsWith("ACH_")) s = s.Substring(4);
            if (s.EndsWith("_Title")) s = s.Substring(0, s.Length - 6);
            return Regex.Replace(s, "(?<!^)(?=[A-Z])", " ");
        }
    }

    public static class Toasts
    {
        class T { public string Text; public float Expire; }
        static readonly List<T> list = new List<T>();
        public static void Add(string text)
        {
            list.Add(new T { Text = text, Expire = Time.realtimeSinceStartup + 4f });
            if (list.Count > 8) list.RemoveAt(0);
        }
        public static void Draw(GUIStyle style, float x, float y)
        {
            float now = Time.realtimeSinceStartup;
            list.RemoveAll(t => t.Expire < now);
            foreach (var t in list)
            {
                float a = Mathf.Clamp01((t.Expire - now) / 1f);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                GUI.Label(new Rect(x, y, 700, 24), t.Text, style);
                GUI.color = old;
                y += 22;
            }
        }
    }
}
