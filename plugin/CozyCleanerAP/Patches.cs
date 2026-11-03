using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyCleanerAP
{
    /// <summary>Cached private-field reader.</summary>
    public static class Fld
    {
        static readonly Dictionary<string, FieldInfo> cache = new Dictionary<string, FieldInfo>();
        public static int Int(object o, string name)
        {
            try
            {
                if (o == null) return -1;
                string key = o.GetType().FullName + "." + name;
                FieldInfo f;
                if (!cache.TryGetValue(key, out f)) { f = AccessTools.Field(o.GetType(), name); cache[key] = f; }
                if (f == null) return -1;
                return (int)f.GetValue(o);
            }
            catch { return -1; }
        }
    }

    public static class Units
    {
        /// <summary>Counter went from before to after: send "{N} - {verb} #k" for each new k.</summary>
        public static void Report(int before, int after)
        {
            try
            {
                if (!Bridge.Connected || before < 0 || after <= before || after - before > 500) return;
                var lv = Bridge.CurrentLevel();
                if (lv == null) return;
                var names = new List<string>();
                for (int k = before + 1; k <= after; k++) names.Add(lv.name + " - " + lv.verb + " #" + k);
                Bridge.SendMany(names, quiet: names.Count > 1);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] unit: " + e.Message); }
        }

        public static void Stage(int n)
        {
            try
            {
                if (!Bridge.Connected) return;
                var lv = Bridge.CurrentLevel();
                if (lv == null) return;
                Bridge.Send(lv.name + " - Stage " + n + " Done");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] stage: " + e.Message); }
        }

        public static void LevelComplete()
        {
            try
            {
                if (!Bridge.Connected) return;
                var lv = Bridge.CurrentLevel();
                if (lv == null) { Plugin.Log.LogWarning("[CozyAP] level completed but level unknown"); return; }
                var names = new List<string> { lv.name + " - Complete" };
                if (lv.HasStages) { names.Add(lv.name + " - Stage 1 Done"); names.Add(lv.name + " - Stage 2 Done"); }
                for (int k = 1; k <= lv.total; k++) names.Add(lv.name + " - " + lv.verb + " #" + k);
                Bridge.SendMany(names, quiet: true);

                var s = Bridge.Slot;
                bool goal;
                if (s.goal == "levels_completed")
                    goal = s.levels.Count(l => l.id != 1 && Bridge.Has(l.name + " - Complete")) >= s.levels_needed;
                else
                    goal = lv.id == 36;
                if (goal) Bridge.SendGoal();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] complete: " + e.Message); }
        }
    }

    // ===================================================================== save isolation
    [HarmonyPatch(typeof(SaveManager), "EnsurePaths")]
    static class P_SaveEnsurePaths
    {
        static bool decided, vanilla;
        static void Postfix(SaveManager __instance)
        {
            try
            {
                if (!decided)
                {
                    decided = true;
                    if (!Bridge.Connected || Bridge.Dir == null)
                    {
                        vanilla = true;
                        Bridge.SaveManagerBooted = true;
                        Plugin.Log.LogWarning("[CozyAP] Archipelago client was not running when the game started: using the NORMAL save. Start the client before the game for an isolated AP save.");
                        return;
                    }
                    Bridge.FrozenSaveDir = Bridge.SaveDir;
                    Directory.CreateDirectory(Bridge.FrozenSaveDir);
                    Plugin.Log.LogInfo("[CozyAP] save redirected to " + Bridge.FrozenSaveDir);
                    Bridge.SaveManagerBooted = true;
                }
                if (vanilla) return;
                string d = Bridge.FrozenSaveDir;
                var t = typeof(SaveManager);
                AccessTools.Field(t, "savePath").SetValue(__instance, Path.Combine(d, "save.json"));
                AccessTools.Field(t, "backupPath").SetValue(__instance, Path.Combine(d, "save.bak"));
                AccessTools.Field(t, "legacyBackupPath").SetValue(__instance, Path.Combine(d, "save.json.bak"));
                AccessTools.Field(t, "metaPath").SetValue(__instance, Path.Combine(d, "save.meta"));
                AccessTools.Field(t, "logPath").SetValue(__instance, Path.Combine(d, "save_log.txt"));
                Bridge.SaveIsolated = true;
            }
            catch (Exception e) { Plugin.Log.LogError("[CozyAP] EnsurePaths: " + e); }
        }
    }

    // ===================================================================== PlayerPrefs isolation (progress keys only)
    static class PrefsIso
    {
        public static bool Iso(string k)
        {
            if (Bridge.ActiveSaveDir == null || k == null) return false;
            return k.StartsWith("Level_") || k.StartsWith("LevelDone_") || k.StartsWith("MiniGameDone_") || k.StartsWith("Stat_")
                || k == "LastPlayedLevel" || k == "TargetCamLevel" || k == "HasSavedGame" || k == "OpenShopOnLoad";
        }
    }

    [HarmonyPatch(typeof(PlayerPrefs), "GetInt", new[] { typeof(string), typeof(int) })]
    static class P_PrefsGetInt
    {
        static bool Prefix(object[] __args, ref int __result)
        {
            string k = (string)__args[0];
            if (!PrefsIso.Iso(k)) return true;
            int v;
            __result = Bridge.Prefs.TryGetValue(k, out v) ? v : (int)__args[1];
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerPrefs), "SetInt", new[] { typeof(string), typeof(int) })]
    static class P_PrefsSetInt
    {
        static bool Prefix(object[] __args)
        {
            string k = (string)__args[0];
            if (!PrefsIso.Iso(k)) return true;
            Bridge.Prefs[k] = (int)__args[1];
            Bridge.PrefsDirty();
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerPrefs), "HasKey", new[] { typeof(string) })]
    static class P_PrefsHasKey
    {
        static bool Prefix(object[] __args, ref bool __result)
        {
            string k = (string)__args[0];
            if (!PrefsIso.Iso(k)) return true;
            __result = Bridge.Prefs.ContainsKey(k);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerPrefs), "DeleteKey", new[] { typeof(string) })]
    static class P_PrefsDeleteKey
    {
        static bool Prefix(object[] __args)
        {
            string k = (string)__args[0];
            if (!PrefsIso.Iso(k)) return true;
            Bridge.Prefs.Remove(k);
            Bridge.PrefsDirty();
            return false;
        }
    }

    // ===================================================================== cloud progress isolation
    // LevelProgressCloudSync mirrors level progress to persistentDataPath/cloud_levels.json; keep AP in its own file.
    static class CloudIso
    {
        public static void Redirect(ref string cloudPath)
        {
            try
            {
                string d = Bridge.ActiveSaveDir;
                if (d == null) return;
                Directory.CreateDirectory(d);
                cloudPath = Path.Combine(d, "cloud_levels.json");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] cloud path: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(LevelProgressCloudSync), "RestoreFromCloud")]
    static class P_CloudRestore { static void Prefix(ref string ___cloudPath) { CloudIso.Redirect(ref ___cloudPath); } }

    [HarmonyPatch(typeof(LevelProgressCloudSync), "FlushToCloud")]
    static class P_CloudFlush { static void Prefix(ref string ___cloudPath) { CloudIso.Redirect(ref ___cloudPath); } }

    // ===================================================================== level gating
    [HarmonyPatch(typeof(LevelHouse), "CheckLevelStatus")]
    static class P_LevelHouseStatus
    {
        // Replaces the vanilla check (PlayerPrefs "highest unlocked") with key ownership.
        static bool Prefix(LevelHouse __instance)
        {
            try
            {
                if (!Bridge.Connected) return true;
                var h = __instance;
                if (h.isStore || h.levelIndex == 1) h.isLocked = false;
                else
                {
                    var lv = Bridge.LevelById(h.levelIndex);
                    h.isLocked = !(lv != null && Bridge.HasItem(lv.name + " Key"));
                }
                AccessTools.Method(typeof(LevelHouse), "UpdateVisuals").Invoke(h, null);
                return false;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] LevelHouse: " + e.Message); return true; }
        }
    }

    [HarmonyPatch]
    static class P_LoadScene
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var t = typeof(SceneManager);
            var list = new[]
            {
                AccessTools.Method(t, "LoadScene", new[] { typeof(string) }),
                AccessTools.Method(t, "LoadScene", new[] { typeof(string), typeof(LoadSceneMode) }),
                AccessTools.Method(t, "LoadScene", new[] { typeof(int) }),
                AccessTools.Method(t, "LoadScene", new[] { typeof(int), typeof(LoadSceneMode) }),
            };
            return list.Where(m => m != null).Cast<MethodBase>();
        }

        static bool redirecting;
        static bool Prefix(object[] __args)
        {
            try
            {
                if (!Bridge.Connected || redirecting) return true;
                LevelInfo lv = __args[0] is string ? Bridge.LevelByScene((string)__args[0]) : Bridge.LevelByBuild((int)__args[0]);
                if (lv == null || lv.id == 1 || Bridge.HasItem(lv.name + " Key")) return true;
                Toasts.Add("Locked: need " + lv.name + " Key");
                redirecting = true;
                try { SceneManager.LoadScene("LevelSelectScene"); } finally { redirecting = false; }
                return false;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] LoadScene: " + e.Message); return true; }
        }
    }

    // ===================================================================== completion / units / stages
    [HarmonyPatch(typeof(level_controller), "ShowCompletionUI")]
    static class P_Complete
    {
        static void Prefix() { Units.LevelComplete(); }
    }

    [HarmonyPatch(typeof(CleaningProgress), "ObjectCleaned")]
    static class P_UnitHouse
    {
        static void Prefix(CleaningProgress __instance, out int __state) { __state = Fld.Int(__instance, "cleanedObjects"); }
        static void Postfix(CleaningProgress __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "cleanedObjects")); }
    }

    [HarmonyPatch(typeof(CleaningProgressForRecycling), "ObjectCleaned")]
    static class P_UnitRecycle
    {
        static void Prefix(CleaningProgressForRecycling __instance, out int __state) { __state = Fld.Int(__instance, "cleanedCount"); }
        static void Postfix(CleaningProgressForRecycling __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "cleanedCount")); }
    }

    [HarmonyPatch(typeof(CleaningProgressForRepair), "PiecePlaced")]
    static class P_UnitRepair
    {
        static void Prefix(CleaningProgressForRepair __instance, out int __state) { __state = Fld.Int(__instance, "placedPieces"); }
        static void Postfix(CleaningProgressForRepair __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "placedPieces")); }
    }

    [HarmonyPatch(typeof(CleaningProgressForFlower), "IncrementStep")]
    static class P_UnitFlower
    {
        static void Prefix(CleaningProgressForFlower __instance, out int __state) { __state = Fld.Int(__instance, "completedSteps"); }
        static void Postfix(CleaningProgressForFlower __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "completedSteps")); }
    }

    [HarmonyPatch(typeof(CleaningProgressDirty), "CheckProgress")]
    static class P_UnitDirty
    {
        static void Prefix(CleaningProgressDirty __instance, out int __state) { __state = Fld.Int(__instance, "cleanedCount"); }
        static void Postfix(CleaningProgressDirty __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "cleanedCount")); }
    }

    [HarmonyPatch(typeof(LawnCleaningProgress), "HandleGrassCut")]
    static class P_UnitLawn
    {
        static void Prefix(LawnCleaningProgress __instance, out int __state) { __state = Fld.Int(__instance, "cutCount"); }
        static void Postfix(LawnCleaningProgress __instance, int __state) { Units.Report(__state, Fld.Int(__instance, "cutCount")); }
    }

    // ===================================================================== achievements
    [HarmonyPatch(typeof(AchievementManager), "Unlock")]
    static class P_AchUnlock
    {
        static void Prefix(string achievementId)
        {
            try
            {
                if (!Bridge.Connected || string.IsNullOrEmpty(achievementId)) return;
                Bridge.Send("Achievement - " + Bridge.AchievementTitle(achievementId));
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] ach: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(AchievementManager), "IsUnlocked")]
    static class P_AchIsUnlocked
    {
        static bool Prefix(string achievementId, ref bool __result)
        {
            if (!Bridge.Connected) return true;
            __result = !string.IsNullOrEmpty(achievementId) && Bridge.Has("Achievement - " + Bridge.AchievementTitle(achievementId));
            return false;
        }
    }

    // ===================================================================== tools
    [HarmonyPatch(typeof(ToolManager), "LoadFromSave")]
    static class P_TmLoad { static void Postfix() { Tools.Enforce(); } }

    [HarmonyPatch(typeof(ToolManager), "SaveToSaveFile")]
    static class P_TmSave { static void Postfix() { Tools.Enforce(); } }

    [HarmonyPatch(typeof(ToolManager), "RepairUnlockedToolsFromStages")]
    static class P_TmRepair { static void Postfix() { Tools.Enforce(); } }

    [HarmonyPatch(typeof(ToolManager), "SyncShopFlagsBothWays")]
    static class P_TmSync { static void Postfix() { Tools.Enforce(); } }

    [HarmonyPatch(typeof(CleaningProgressForRecycling), "OnCleaningCompleted")]
    static class P_RecycleDone
    {
        static void Postfix()
        {
            try
            {
                if (Bridge.Connected && AchievementManager.Instance != null) AchievementManager.Instance.ReportMinigameDone("Recycling");
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] recycling achv: " + e.Message); }
            Tools.Enforce();
        }
    }

    [HarmonyPatch(typeof(CleaningProgressForFlower), "OnAllStepsCompleted")]
    static class P_FlowerDone { static void Postfix() { Tools.Enforce(); } }

    [HarmonyPatch(typeof(ToolManager), "SelectTool")]
    static class P_SelectTool
    {
        static bool Prefix(object[] __args)
        {
            try
            {
                if (!Bridge.Connected) return true;
                var t = (CleaningToolType)(int)__args[0];
                if (Tools.Allowed().Contains(t)) return true;
                Toasts.Add(Tools.Name(t) + " not received yet");
                return false;
            }
            catch { return true; }
        }
    }

    // ===================================================================== shop
    [HarmonyPatch(typeof(ShopToolManager), "BuyOrUpgrade")]
    static class P_ShopBuyOrUpgrade
    {
        static bool Prefix(object[] __args, ref bool __result)
        {
            bool ok;
            if (!Tools.HandlePurchase((CleaningToolType)__args[0], out ok)) return true;
            __result = ok;
            return false;
        }
    }

    [HarmonyPatch(typeof(ShopToolManager), "BuyOrUpgradeTool")]
    static class P_ShopBuyOrUpgradeTool
    {
        static bool Prefix(object[] __args)
        {
            bool ok;
            return !Tools.HandlePurchase((CleaningToolType)__args[0], out ok);
        }
    }

    [HarmonyPatch(typeof(ShopToolManager), "TryPurchase")]
    static class P_ShopTryPurchase
    {
        static bool Prefix(object[] __args, ref bool __result)
        {
            bool ok;
            if (!Tools.HandlePurchase((CleaningToolType)__args[0], out ok)) return true;
            __result = ok;
            return false;
        }
    }

    [HarmonyPatch(typeof(ToolUpgradeButton), "Refresh")]
    static class P_ShopButtonRefresh
    {
        // Best effort: button state/price follow the AP purchase count (0,1,2) instead of the real tool stage.
        static void Postfix(ToolUpgradeButton __instance)
        {
            try
            {
                if (!Bridge.Connected || !Bridge.Slot.shopsanity) return;
                var b = __instance;
                // shopsanity: every tool is purchasable from the start, so drop the game's ownership lock
                AccessTools.Field(typeof(ToolUpgradeButton), "isLockedByGame").SetValue(b, false);
                if (b.lockIcon != null) b.lockIcon.SetActive(false);
                int p = Bridge.PurchaseCount(Tools.Name(b.toolType));
                if (p >= 2)
                {
                    AccessTools.Field(typeof(ToolUpgradeButton), "isMaxedOut").SetValue(b, true);
                    b.button.interactable = false;
                    b.localizedLabelText.TableEntryReference = "Max";
                    b.localizedLabelText.RefreshString();
                    return;
                }
                AccessTools.Field(typeof(ToolUpgradeButton), "currentPrice").SetValue(b, p == 0 ? b.stage2Price : b.stage3Price);
                AccessTools.Field(typeof(ToolUpgradeButton), "isMaxedOut").SetValue(b, false);
                b.button.interactable = true;
                b.localizedLabelText.TableEntryReference = "Upgrade";
                b.localizedLabelText.RefreshString();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] shop refresh: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(ToolUpgradeButton), "UpdateTooltipContent")]
    static class P_ShopTooltip
    {
        static void Postfix(ToolUpgradeButton __instance)
        {
            try
            {
                if (!Bridge.Connected || !Bridge.Slot.shopsanity) return;
                var b = __instance;
                if (b.tooltipPriceText == null) return;
                int p = Bridge.PurchaseCount(Tools.Name(b.toolType));
                if (p >= 2) { b.tooltipPriceText.text = b.defaultMaxPriceWord; return; }
                int price = p == 0 ? b.stage2Price : b.stage3Price;
                b.tooltipPriceText.text = price + " ";
                bool poor = MoneyManager.Instance != null && !MoneyManager.Instance.HasEnoughMoney(price);
                b.tooltipPriceText.color = poor ? b.unaffordablePriceColor : b.affordablePriceColor;
            }
            catch { }
        }
    }
}
