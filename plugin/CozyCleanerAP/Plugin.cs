using System;
using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyCleanerAP
{
    [BepInPlugin("cozycleaner.archipelago", "Cozy Cleaner Archipelago", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "cozycleaner.archipelago";
        internal static ManualLogSource Log;
        internal static Plugin Instance;
        float nextPoll;
        GUIStyle style, shadow;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo("[CozyAP] plugin 0.1.0 loading");
            try { Bridge.Init(); } catch (Exception e) { Log.LogError("[CozyAP] Bridge.Init: " + e); }

            var harmony = new Harmony(Guid);
            foreach (var t in typeof(Plugin).Assembly.GetTypes())
            {
                if (!t.GetCustomAttributes(typeof(HarmonyPatch), true).Any()) continue;
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (Exception e) { Log.LogError("[CozyAP] patch " + t.Name + " failed: " + e.Message); }
            }

            try
            {
                CleaningProgress.OnStage1Finished += () => Units.Stage(1);
                CleaningProgress.OnStage2Finished += () => Units.Stage(2);
            }
            catch (Exception e) { Log.LogError("[CozyAP] stage events: " + e.Message); }

            SceneManager.sceneLoaded += OnSceneLoaded;
            // The game destroys BepInEx's manager object early, which kills this component's Update/OnGUI.
            // Run the loop from our own hidden, persistent object instead.
            var go = new GameObject("CozyAP_Runner");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
            Log.LogInfo("[CozyAP] loaded; connected=" + Bridge.Connected);
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            try
            {
                Bridge.WriteStatus();
                Tools.Enforce();
                if (Runner.Instance != null) Runner.Instance.StartCoroutine(LogTotals(s.name));
            }
            catch (Exception e) { Log.LogWarning("[CozyAP] sceneLoaded: " + e); }
        }

        IEnumerator LogTotals(string scene)
        {
            yield return new WaitForSeconds(3f);
            if (SceneManager.GetActiveScene().name != scene) yield break;
            try
            {
                var lv = Bridge.CurrentLevel();
                int id = level_controller.Instance != null ? level_controller.Instance.currentLevelID : -1;
                var cp = FindObjectOfType<CleaningProgress>();
                if (cp != null)
                    Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=house stage1={2} stage2={3} stage3={4} (table total={5} s1={6} s2={7})",
                        id, scene, Fld.Int(cp, "stage1InitialCount"), Fld.Int(cp, "stage2InitialCount"), Fld.Int(cp, "stage3InitialCount"),
                        lv != null ? lv.total : -1, lv != null ? lv.stage1 : -1, lv != null ? lv.stage2 : -1));
                var rc = FindObjectOfType<CleaningProgressForRecycling>();
                if (rc != null) Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=recycling total={2} (table {3})", id, scene, Fld.Int(rc, "totalTrashCount"), lv != null ? lv.total : -1));
                var rp = FindObjectOfType<CleaningProgressForRepair>();
                if (rp != null) Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=repair total={2} (table {3})", id, scene, Fld.Int(rp, "totalPieces"), lv != null ? lv.total : -1));
                var fl = FindObjectOfType<CleaningProgressForFlower>();
                if (fl != null) Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=garden total={2} (table {3})", id, scene, fl.targetStepCount, lv != null ? lv.total : -1));
                var dt = FindObjectOfType<CleaningProgressDirty>();
                if (dt != null) Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=fridge total={2} (table {3})", id, scene, Fld.Int(dt, "totalCount"), lv != null ? lv.total : -1));
                var lw = FindObjectOfType<LawnCleaningProgress>();
                if (lw != null) Log.LogInfo(string.Format("[CozyAP] totals level={0} scene={1} kind=lawn total={2} (table {3})", id, scene, Fld.Int(lw, "initialCount"), lv != null ? lv.total : -1));
            }
            catch (Exception e) { Log.LogWarning("[CozyAP] totals: " + e.Message); }
        }

        internal void TickUpdate()
        {
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 0.5f;
            try
            {
                Bridge.Poll();
                ApplyPending();
                Tools.Enforce();
                Bridge.WriteStatus();
            }
            catch (Exception e) { Log.LogWarning("[CozyAP] Update: " + e.Message); }
        }

        void OnApplicationQuit() { try { Bridge.FlushDirty(); } catch { } }

        static bool Ready(ItemRec it)
        {
            bool saveOk = SaveManager.Instance != null && SaveManager.Instance.data != null;
            if (it.Name == "Money Bundle") return saveOk && MoneyManager.Instance != null;
            if (it.Name.StartsWith("Progressive ") || it.Name == "Cloth" || it.Name == "Watering Can"
                || it.Name == "Dustpan" || it.Name == "Mop" || it.Name == "Vacuum")
                return saveOk && ToolManager.Instance != null;
            return true;
        }

        void ApplyPending()
        {
            if (!Bridge.Connected) return;
            while (Bridge.Pending.Count > 0)
            {
                var it = Bridge.Pending.Peek();
                if (!Ready(it)) break;
                Bridge.Pending.Dequeue();
                try
                {
                    if (it.Name == "Money Bundle")
                    {
                        int amt = 50 * Math.Max(1, Bridge.Slot.money_multiplier);
                        MoneyManager.Instance.AddMoney(amt);
                        Toasts.Add("Received " + amt + " coins from " + it.Player);
                    }
                    else
                    {
                        Toasts.Add("Received " + it.Name + (string.IsNullOrEmpty(it.Player) ? "" : " from " + it.Player));
                        if (it.Name.EndsWith(" Key")) RefreshLevelHouses();
                        else Tools.Enforce();
                    }
                }
                catch (Exception e) { Log.LogWarning("[CozyAP] apply " + it.Name + ": " + e.Message); }
                Bridge.MarkApplied(it.Index);
            }
        }

        internal sealed class Runner : MonoBehaviour
        {
            internal static Runner Instance;
            void Awake() { Instance = this; }
            // (object) comparison: Plugin.Instance is a destroyed Unity object, but its C# methods still work
            void Update() { if ((object)Plugin.Instance != null) Plugin.Instance.TickUpdate(); }
            void OnGUI() { if ((object)Plugin.Instance != null) Plugin.Instance.TickGUI(); }
        }

        internal static void RefreshLevelHouses()
        {
            try
            {
                var m = AccessTools.Method(typeof(LevelHouse), "CheckLevelStatus");
                foreach (var h in FindObjectsOfType<LevelHouse>()) if (h != null) m.Invoke(h, null);
            }
            catch (Exception e) { Log.LogWarning("[CozyAP] refresh houses: " + e.Message); }
        }

        internal void TickGUI()
        {
            if (style == null)
            {
                style = new GUIStyle { fontSize = 15 };
                style.normal.textColor = Color.white;
            }
            if (Bridge.Blocked)
            {
                var o = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.55f);
                GUI.Label(new Rect(10, 6, 900, 24), "Archipelago: restart the game to play seed " + Bridge.BlockedSeed, style);
                GUI.color = o;
                Toasts.Draw(style, 10, 30);
                return;
            }
            string status = Bridge.Connected
                ? "AP: connected to " + Bridge.Slot.seed + " | items " + Bridge.Received.Count
                : "AP: not connected";
            var old = GUI.color;
            GUI.color = Bridge.Connected ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.7f, 0.7f);
            GUI.Label(new Rect(10, 6, 700, 24), status, style);
            GUI.color = old;
            Toasts.Draw(style, 10, 30);
        }
    }
}
