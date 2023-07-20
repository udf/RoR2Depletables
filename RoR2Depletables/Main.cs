using BepInEx;
using RoR2;
using R2API;
using R2API.Utils;
using System.Collections.Generic;
using System.Security.Permissions;
using static RoR2Depletables.Core;
using BepInEx.Configuration;
using System.Linq;
using UnityEngine;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]
#pragma warning restore CS0618 // Type or member is obsolete

namespace RoR2Depletables
{
    [BepInPlugin("com.MagicGonads.RoR2Depletables", "Voidtouched Items", "1.0.5")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod)]
    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency("com.MagicGonads.RoR2TierScaling", BepInDependency.DependencyFlags.SoftDependency)]
    public class Main : BaseUnityPlugin
    {
        public static ConfigEntry<bool> configIconColours;
        public static ConfigEntry<Color> configDepletedColour;
        public static ConfigEntry<bool> configShowLogbook;
        public static ConfigEntry<bool> configMakeDepleted;
        public static ConfigEntry<bool> configDepleteItems;
        public static ConfigEntry<bool> configDepletedItems;
        public static ConfigEntry<bool> configInvertBlacklist;
        public static ConfigEntry<string> configItemBlacklist;
        public static ConfigEntry<string> configLanguage;

        public void Awake()
        {
           configShowLogbook = Config.Bind(
                "0. Main",
                "Show In Logbook",
                false,
                "Allows the depleted variants of the items to be shown in the logbook."
            );

            configIconColours = Config.Bind(
                "0. Main",
                "Reshaded Icon Colours",
                true,
                "Changes the depleted item's icon colours to differentiate them from their originals."
            );

            configDepletedColour = Config.Bind(
                "0. Main",
                "Depleted Colour",
                new Color(0.8f,0.1f,0.6f),
                "The colour to mix in with the original colour for depleted items."
            );

            configMakeDepleted = Config.Bind(
                "0. Main",
                "Make Depleted Items",
                true,
                "Required for most of the mod to function, generates variants of each item that they can be depleted to."
            );

            configDepleteItems = Config.Bind(
                "0. Main",
                "Deplete Items",
                true,
                "Whether items are depleted instead of being destroyed when corrupted."
            );

            configDepletedItems = Config.Bind(
                "0. Main",
                "Handle Depleted Items",
                true,
                "Depleted items have their original effect (no effect otherwise)."
            );

            configInvertBlacklist = Config.Bind(
                "0. Main",
                "Item Blacklist Is Whitelist",
                false,
                "Makes the item blacklist act as a whitelist instead, so only those items are included rather than excluded."
            );

            configItemBlacklist = Config.Bind(
                "0. Main",
                "Item Blacklist",
                "",
                "Semicolon (';') separated tokens / codenames / names of items that should be excluded from depletion."
            );

            On.RoR2.ItemTierCatalog.Init += (orig) =>
            {
                On.RoR2.ItemCatalog.SetItemDefs += (_orig, items) =>
                {
                    _orig.Invoke(items);
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                    LateOnItemCatalogSetItemDefs(ItemCatalog.itemDefs);
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
                };
                var tiers = RoR2.ContentManagement.ContentManager._itemTierDefs;
                tiers = OnItemTierCatalogInit(tiers);
                RoR2.ContentManagement.ContentManager._itemTierDefs = tiers;
                orig.Invoke();
            };

            On.RoR2.ItemCatalog.SetItemDefs += (orig, items) =>
            {
                var languages = string.Join(", ",Language.GetAllLanguages().Select(s=>s.name));
                configLanguage = Config.Bind(
                    "0. Main",
                    "Language For Names",
                    "en",
                    "The language used to check the names of items (must be the same for all users!). Valid language options: " + languages
                );

                if (configItemBlacklist.Value.Length > 0)
                    foreach (var p in configItemBlacklist.Value.Split(';').Select(s => s.Trim().ToLower()))
                        excludedItems.Add(p);

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
                var entries = orig.Invoke(exps);
                foreach (var action in delayedLanguage) action.Invoke();
                if (configShowLogbook.Value) return entries;
                return entries.Where(e => !depletedTokens.ContainsKey(e.nameToken)).ToArray();
            };

            On.RoR2.Items.ContagiousItemManager.StepInventoryInfection += (orig, inv, item, limit, forced) =>
            {
                if (configDepleteItems.Value)
                {
                    if (!forced && depleted.Contains(ItemCatalog.GetItemDef(item))) return false;
                    OnContagiousItemManagerStepInventoryInfection(inv, item, limit);
                } 
                return orig.Invoke(inv, item, limit, forced);
            };

            On.RoR2.Inventory.GetItemCount_ItemDef += (orig, inv, item) =>
            {
                if (configDepletedItems.Value && !doOriginalItemCount && item != null)
                {
                    if (depletion.TryGetValue(item, out var ditem))
                        return orig.Invoke(inv, item) + orig.Invoke(inv, ditem);
                    else if (depleted.Contains(item)) return 0;
                }
                return orig.Invoke(inv, item);
            };

            On.RoR2.Inventory.RemoveItem_ItemDef_int += (orig, inv, item, amount) =>
            {
                if (configDepletedItems.Value && !doOriginalItemCount && item != null 
                    && depletion.TryGetValue(item, out var ditem))
                {
                    doOriginalItemCount = true;
                    var i = inv.GetItemCount(item);
                    if (i < amount && i + inv.GetItemCount(ditem) >= amount)
                    {
                        orig.Invoke(inv, item, i);
                        orig.Invoke(inv, ditem, amount - i);
                    }
                    doOriginalItemCount = false;
                }
                else orig.Invoke(inv, item, amount);
            };
        }

    }
}
