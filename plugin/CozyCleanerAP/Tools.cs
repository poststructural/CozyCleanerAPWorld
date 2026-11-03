using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CozyCleanerAP
{
    public static class Tools
    {
        static bool inEnforce;

        public static readonly CleaningToolType[] All =
        {
            CleaningToolType.Hand, CleaningToolType.Dustpan, CleaningToolType.Cloth,
            CleaningToolType.Mop, CleaningToolType.Watering_Can, CleaningToolType.supurge
        };

        public static string Name(CleaningToolType t)
        {
            switch (t)
            {
                case CleaningToolType.Watering_Can: return "Watering Can";
                case CleaningToolType.supurge: return "Vacuum";
                default: return t.ToString();
            }
        }

        public static bool InTutorial()
        {
            try
            {
                if (level_controller.Instance == null) return false;
                return level_controller.Instance.isTutorialLevel || level_controller.Instance.currentLevelID == 1;
            }
            catch { return false; }
        }

        public static HashSet<CleaningToolType> Allowed()
        {
            var set = new HashSet<CleaningToolType> { CleaningToolType.Hand };
            var s = Bridge.Slot;
            if (s == null) { foreach (var t in All) set.Add(t); return set; }
            if (!s.toolsanity)
            {
                set.Add(CleaningToolType.Dustpan); set.Add(CleaningToolType.Mop); set.Add(CleaningToolType.supurge);
            }
            else
            {
                if (Bridge.HasItem("Dustpan")) set.Add(CleaningToolType.Dustpan);
                if (Bridge.HasItem("Mop")) set.Add(CleaningToolType.Mop);
                if (Bridge.HasItem("Vacuum")) set.Add(CleaningToolType.supurge);
            }
            if (Bridge.HasItem("Cloth")) set.Add(CleaningToolType.Cloth);
            if (Bridge.HasItem("Watering Can")) set.Add(CleaningToolType.Watering_Can);
            if (InTutorial())
            {
                set.Add(CleaningToolType.Dustpan); set.Add(CleaningToolType.Mop); set.Add(CleaningToolType.supurge);
            }
            return set;
        }

        /// <summary>Stage implied by received Progressive items (only meaningful with shopsanity).</summary>
        public static ToolStage TargetStage(CleaningToolType t)
        {
            int n = Math.Min(2, Bridge.Count("Progressive " + Name(t) + " Upgrade"));
            return (ToolStage)(1 + n);
        }

        /// <summary>Force ToolManager / SaveData to exactly the AP tool set. Idempotent, cheap.</summary>
        public static void Enforce()
        {
            if (!Bridge.Connected || inEnforce) return;
            var tm = ToolManager.Instance;
            var sm = SaveManager.Instance;
            if (tm == null || sm == null || sm.data == null) return;
            inEnforce = true;
            try
            {
                var allowed = Allowed();
                bool shop = Bridge.Slot.shopsanity;
                bool changed = false;

                if (ToolManager.unlockedTools == null) { ToolManager.unlockedTools = new List<CleaningToolType>(); changed = true; }
                var ut = ToolManager.unlockedTools;
                changed |= ut.RemoveAll(t => !allowed.Contains(t)) > 0;
                foreach (var a in allowed) if (!ut.Contains(a)) { ut.Add(a); changed = true; }

                var data = sm.data;
                if (data.unlockedTools == null) data.unlockedTools = new List<CleaningToolType>();
                if (data.toolStages == null) data.toolStages = new List<ToolSaveEntry>();
                changed |= data.unlockedTools.RemoveAll(t => !allowed.Contains(t)) > 0;
                foreach (var a in allowed) if (!data.unlockedTools.Contains(a)) { data.unlockedTools.Add(a); changed = true; }

                // in-memory stages
                foreach (var k in tm.toolStages.Keys.ToList())
                    if (!allowed.Contains(k)) { tm.toolStages.Remove(k); changed = true; }
                foreach (var a in allowed)
                {
                    ToolStage cur;
                    if (!tm.toolStages.TryGetValue(a, out cur)) { tm.toolStages[a] = ToolStage.Stage1; cur = ToolStage.Stage1; changed = true; }
                    if (shop && cur > TargetStage(a)) { tm.toolStages[a] = TargetStage(a); changed = true; }
                }
                // saved stages = in-memory stages for allowed tools
                changed |= data.toolStages.RemoveAll(e => e == null || !allowed.Contains(e.toolType)) > 0;
                foreach (var a in allowed)
                {
                    var e = data.toolStages.FirstOrDefault(x => x.toolType == a);
                    if (e == null) { data.toolStages.Add(new ToolSaveEntry { toolType = a, stage = tm.toolStages[a] }); changed = true; }
                    else if (e.stage != tm.toolStages[a]) { e.stage = tm.toolStages[a]; changed = true; }
                }

                // shopsanity: all six tools are shop-purchasable from the start (checks, not ownership)
                bool cloth = shop || allowed.Contains(CleaningToolType.Cloth), can = shop || allowed.Contains(CleaningToolType.Watering_Can);
                if (data.isClothVisibleInShop != cloth) { data.isClothVisibleInShop = cloth; changed = true; }
                if (data.isWateringCanVisibleInShop != can) { data.isWateringCanVisibleInShop = can; changed = true; }

                if (ToolManager.currentTool != CleaningToolType.None && !allowed.Contains(ToolManager.currentTool))
                {
                    ToolManager.currentTool = CleaningToolType.Hand;
                    changed = true;
                }

                // Upgrades received through Progressive items: go through the game so its achievements fire.
                // Tools not owned yet stay pending (derived from the item counts) and are applied on every
                // Enforce/scene load as soon as the tool is owned.
                if (shop)
                {
                    foreach (var a in All)
                    {
                        if (!allowed.Contains(a)) continue;
                        var target = TargetStage(a);
                        if (tm.GetToolStage(a) < target) { tm.SetToolStage(a, target); changed = true; }
                    }
                    // SetToolStage adds the tool to unlockedTools as a side effect; undo for anything not owned
                    if (ToolManager.unlockedTools != null && ToolManager.unlockedTools.RemoveAll(t => !allowed.Contains(t)) > 0) changed = true;
                    if (data.unlockedTools.RemoveAll(t => !allowed.Contains(t)) > 0) changed = true;
                }

                if (changed)
                {
                    try { tm.RefreshAllVisuals(); } catch { }
                    try { var cb = ToolManager.OnToolDataChanged; if (cb != null) cb(); } catch { }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] Enforce: " + e); }
            finally { inEnforce = false; }
        }

        // ---------------------------------------------------------------- shop
        public static int ButtonPrice(CleaningToolType t, int purchase)
        {
            try
            {
                foreach (var b in UnityEngine.Object.FindObjectsOfType<ToolUpgradeButton>())
                    if (b != null && b.toolType == t) return purchase == 0 ? b.stage2Price : b.stage3Price;
            }
            catch { }
            return purchase == 0 ? 250 : 500;
        }

        /// <summary>Returns true when the purchase was handled by AP (original must be skipped).</summary>
        public static bool HandlePurchase(CleaningToolType t, out bool success)
        {
            success = false;
            if (!Bridge.Connected || !Bridge.Slot.shopsanity) return false;
            try
            {
                string name = Name(t);
                int n = Bridge.PurchaseCount(name);
                if (n >= 2 || MoneyManager.Instance == null) { return true; }
                int price = ButtonPrice(t, n);
                if (!MoneyManager.Instance.SpendMoney(price))
                {
                    Toasts.Add("Not enough money (" + price + ")");
                    return true;
                }
                Bridge.SetPurchaseCount(name, n + 1);
                // the check goes into the persisted outbox (and checks.jsonl when writable); FlushDirty makes the purchase durable now
                Bridge.Send("Shop - " + name + " Upgrade " + (n + 1));
                Bridge.FlushDirty();
                success = true;
                RefreshShopUi();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[CozyAP] HandlePurchase: " + e); }
            return true;
        }

        public static void RefreshShopUi()
        {
            try
            {
                foreach (var b in UnityEngine.Object.FindObjectsOfType<ToolUpgradeButton>())
                    if (b != null) AccessTools.Method(typeof(ToolUpgradeButton), "Refresh").Invoke(b, null);
            }
            catch { }
        }
    }
}
