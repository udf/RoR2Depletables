using BepInEx;
using RoR2;
using RoR2.Items;
using R2API;
using R2API.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static RoR2Depletables.Utils;
using UnityEngine.AddressableAssets;
using HarmonyLib;
using RoR2.ExpansionManagement;

namespace RoR2Depletables
{
    public static class Core
    {
        public class DepletedItemTier : ItemTierDef
        {
            public static List<ItemTag> exceptTags = new List<ItemTag> {ItemTag.Scrap, ItemTag.PriorityScrap};
            public static List<ItemTag> concatTags = new List<ItemTag> {ItemTag.Cleansable,ItemTag.AIBlacklist};

            public static ItemTag? customTag = null;

            public static Dictionary<ItemTier, DepletedItemTier> cache = new Dictionary<ItemTier, DepletedItemTier>();

            public static DepletedItemTier Get(ItemTier tier)
            {
                if (customTag is null)
                {
                    customTag = ItemAPI.AddItemTag("Depleted");
                    concatTags.Add(customTag.Value);
                }
                return cache.TryGetValue(tier, out var d) ? d : CreateInstance<DepletedItemTier>().Init(tier);
            }

            public DepletedItemTier Init(ItemTier tier)
            {
                cache.Add(tier, this);

                name = "Depletable_" + Enum.GetName(typeof(ItemTier), tier);

                this.tier = ItemTier.AssignedAtRuntime;  
                isDroppable = false;
                canScrap = false;
                canRestack = false;
                pickupRules = PickupRules.Default;

                switch (tier)
                {
                    case ItemTier.Tier1:
                        colorIndex = ColorCatalog.ColorIndex.Tier1Item;
                        darkColorIndex = ColorCatalog.ColorIndex.Tier1ItemDark;
                        bgIconTexture = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier1Def.asset").WaitForCompletion()?.bgIconTexture;
                        dropletDisplayPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier1Def.asset").WaitForCompletion()?.dropletDisplayPrefab;
                        highlightPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier1Def.asset").WaitForCompletion()?.highlightPrefab;
                        break;
                    case ItemTier.Tier2:
                        colorIndex = ColorCatalog.ColorIndex.Tier2Item;
                        darkColorIndex = ColorCatalog.ColorIndex.Tier2ItemDark;
                        bgIconTexture = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier2Def.asset").WaitForCompletion()?.bgIconTexture;
                        dropletDisplayPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier2Def.asset").WaitForCompletion()?.dropletDisplayPrefab;
                        highlightPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier2Def.asset").WaitForCompletion()?.highlightPrefab;
                        break;
                    case ItemTier.Tier3:
                        colorIndex = ColorCatalog.ColorIndex.Tier3Item;
                        darkColorIndex = ColorCatalog.ColorIndex.Tier3ItemDark;
                        bgIconTexture = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier3Def.asset").WaitForCompletion()?.bgIconTexture;
                        dropletDisplayPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier3Def.asset").WaitForCompletion()?.dropletDisplayPrefab;
                        highlightPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/Tier3Def.asset").WaitForCompletion()?.highlightPrefab;
                        break;
                    case ItemTier.Boss:
                        colorIndex = ColorCatalog.ColorIndex.BossItem;
                        darkColorIndex = ColorCatalog.ColorIndex.BossItemDark;
                        bgIconTexture = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/BossDef.asset").WaitForCompletion()?.bgIconTexture;
                        dropletDisplayPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/BossDef.asset").WaitForCompletion()?.dropletDisplayPrefab;
                        highlightPrefab = Addressables.LoadAssetAsync<ItemTierDef>
                            ("RoR2/Base/Common/BossDef.asset").WaitForCompletion()?.highlightPrefab;
                        break;
                    default:
                        colorIndex = ColorCatalog.ColorIndex.WIP;
                        darkColorIndex = ColorCatalog.ColorIndex.WIP;
                        bgIconTexture = null;
                        highlightPrefab = null;
                        dropletDisplayPrefab = null;
                        break;
                }

                return this;
            }
        }

        public static Dictionary<ItemDef, CustomItem> depletion = new Dictionary<ItemDef, CustomItem>();
        public static HashSet<ItemDef> depleted = new HashSet<ItemDef>();

        public static ItemDef[]  OnItemCatalogSetItemDefs(ItemDef[] items)
        {
            var litems = items.ToList();
            //var i = items.Count();
            foreach (var item in items)
            {
                Debug.LogWarning("ONSETDEF: " + item.name);
                var ditem = MakeDepletableItem(item);
                if (ItemAPI.Add(ditem))
                {
                    Debug.LogWarning("ADD: " + ditem.ItemDef.name);
                    //ditem.ItemDef.itemIndex = (ItemIndex)(++i);
                    depletion.Add(item, ditem);
                    depleted.Add(ditem.ItemDef);
                    litems.Add(ditem.ItemDef);
                }
            }
            return litems.ToArray();
        }

        public static void OnGenerateRuntimeValues(ItemDisplayRuleSet rules)
        {
            //foreach (var g in rules.keyAssetRuleGroups)
            //    if (g.keyAsset is ItemDef item && depletion.TryGetValue(item, out var ditem))
            //    {
            //        Debug.LogWarning("UPDATEDDISPLAY: " + ditem.ItemDef.name);
            //        var _rules = g.displayRuleGroup.rules;
            //        if (ditem.ItemDisplayRules is null)
            //            ditem.ItemDisplayRules = new ItemDisplayRuleDict(_rules);
            //        else ditem.ItemDisplayRules = new ItemDisplayRuleDict(ditem
            //                .ItemDisplayRules.DefaultRules.AddRangeToArray(_rules));
            //    }
        }

        public static bool doOriginalItemCount = false;

        public static void OnContagiousItemManagerStepInventoryInfection(Inventory inventory, ItemIndex original, int limit)
        {
            var item = ItemCatalog.GetItemDef(original);
            if (depletion.TryGetValue(item,out var ditem))
            {
                Debug.LogWarning("CONVERT: " + ditem.ItemDef.name);
                doOriginalItemCount = true;
                var count = inventory.GetItemCount(item);
                doOriginalItemCount = false;
                inventory.GiveItem(ditem.ItemDef, Math.Min(count,limit));
            }
        }

        public static CustomItem MakeDepletableItem(ItemDef item, ItemDisplayRule[] rules = null)
        {
            rules = rules?.ToArray();

            var tier = DepletedItemTier.Get(item.tier);

            var tags = item.tags.Except(DepletedItemTier.exceptTags)
                .Concat(DepletedItemTier.concatTags).Distinct().ToArray();

            var name = "Depleted" + item.name;

            var ditem = new CustomItem(
                name, item.nameToken, item.descriptionToken, 
                item.loreToken, item.pickupToken, item.pickupIconSprite, 
                item.pickupModelPrefab, tags, item.tier, true, 
                item.canRemove, null, rules, tier);

            ItemCatalog.availability.CallWhenAvailable(() =>
            {
                //ditem.ItemDef.itemIndex = ItemCatalog.FindItemIndex(item.name);
                ditem.ItemDef.pickupModelPrefab = item.pickupModelPrefab;
                ditem.ItemDef.requiredExpansion = item.requiredExpansion;
            });

            return ditem;
        }

    }
}
