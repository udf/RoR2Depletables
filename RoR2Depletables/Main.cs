using BepInEx;
using RoR2;
using R2API;
using R2API.Utils;
using System.Collections.Generic;
using System.Security.Permissions;
using static RoR2Depletables.Core;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]
#pragma warning restore CS0618 // Type or member is obsolete

namespace RoR2Depletables
{
    [BepInPlugin("com.MagicGonads.RoR2Depletables", "Voidtouched Items", "1.0.3")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod)]
    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    public class Main : BaseUnityPlugin
    {

        public void Awake()
        {
            On.RoR2.ItemTierCatalog.Init += (orig) =>
            {
                ref var tiers = ref RoR2.ContentManagement.ContentManager._itemTierDefs;
                tiers = OnItemTierCatalogInit(tiers);
                orig.Invoke();
            };

            On.RoR2.ItemCatalog.SetItemDefs += (orig, items) =>
            {
                items = OnItemCatalogSetItemDefs(items);
                orig.Invoke(items);
            };

            On.RoR2.ItemDisplayRuleSet.GenerateRuntimeValues += (orig,rules) =>
            {
                orig.Invoke(rules);
                OnGenerateRuntimeValues(rules);
            };

            On.RoR2.UI.LogBook.LogBookController.BuildPickupEntries += (orig,exps) =>
            {
                var entries = new List<RoR2.UI.LogBook.Entry>(); 
                foreach (var entry in orig.Invoke(exps))
                    if (depletedTokens.ContainsKey(entry.nameToken) 
                        && delayedLanguage.TryGetValue(entry.nameToken, out var action))
                        action.Invoke();
                    else entries.Add(entry);
                return entries.ToArray();
            };

            On.RoR2.Items.ContagiousItemManager.StepInventoryInfection += (orig, inv, item, limit, forced) =>
            {
                if (!forced && depleted.Contains(ItemCatalog.GetItemDef(item))) return false;
                OnContagiousItemManagerStepInventoryInfection(inv, item, limit);
                return orig.Invoke(inv, item, limit, forced);
            };

            On.RoR2.Inventory.GetItemCount_ItemDef += (orig, inv, item) =>
            {
                if (!doOriginalItemCount && item != null)
                {
                    if (depletion.TryGetValue(item, out var ditem))
                        return orig.Invoke(inv, item) + orig.Invoke(inv, ditem.ItemDef);
                    else if (depleted.Contains(item)) return 0;
                }
                return orig.Invoke(inv, item);
            };

            On.RoR2.Inventory.RemoveItem_ItemDef_int += (orig, inv, item, amount) =>
            {
                if (!doOriginalItemCount && item != null && depletion.TryGetValue(item, out var ditem))
                {
                    doOriginalItemCount = true;
                    var i = inv.GetItemCount(item);
                    if (i < amount && i + inv.GetItemCount(ditem.ItemDef) >= amount)
                    {
                        orig.Invoke(inv, item, i);
                        orig.Invoke(inv, ditem.ItemDef, amount - i);
                    }
                    doOriginalItemCount = false;
                }
                orig.Invoke(inv, item, amount);
            };
        }

    }
}
