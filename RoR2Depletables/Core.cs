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
using static RoR2Depletables.Core;

namespace RoR2Depletables
{
    public static class Core
    {
        public class DepletedItemTier : ItemTierDef
        {
            public static Dictionary<ItemTierDef, DepletedItemTier> cache = new Dictionary<ItemTierDef, DepletedItemTier>();

            public static DepletedItemTier Get(ItemTierDef tier)
            {
                return cache.TryGetValue(tier, out var d) ? d : CreateInstance<DepletedItemTier>().Init(tier);
            }

            public static DepletedItemTier New(ItemTierDef tier)
            {
                return cache.ContainsKey(tier) ? null : CreateInstance<DepletedItemTier>().Init(tier);
            }

            public DepletedItemTier Init(ItemTierDef tier)
            {
                cache.Add(tier, this);

                name = "Depleted" + tier.name;

                this.tier = ItemTier.AssignedAtRuntime;  
                isDroppable = false;
                canScrap = false;
                canRestack = false;
                pickupRules = PickupRules.Default;

                colorIndex = tier.colorIndex;
                darkColorIndex = tier.darkColorIndex;
                bgIconTexture = tier.bgIconTexture;
                highlightPrefab = tier.highlightPrefab;
                dropletDisplayPrefab = tier.dropletDisplayPrefab;

                return this;
            }
        }

        public static List<ItemTag> exceptTags = new List<ItemTag> {ItemTag.Scrap, ItemTag.PriorityScrap};
        public static List<ItemTag> concatTags = new List<ItemTag> {ItemTag.Cleansable,ItemTag.AIBlacklist};

        public static ItemTag? customTag = null;

        public static ItemTag[] GenTags(ItemTag[] tags)
        {
            if (customTag is null)
            {
                customTag = ItemAPI.AddItemTag("Depleted");
                concatTags.Add(customTag.Value);
            }
            return tags.Except(exceptTags).Concat(concatTags).Distinct().ToArray();
        }

        public static Dictionary<ItemDef, CustomItem> depletion = new Dictionary<ItemDef, CustomItem>();
        public static HashSet<ItemDef> depleted = new HashSet<ItemDef>();

        public static ItemTierDef[] OnItemTierCatalogInit(ItemTierDef[] tiers)
        {
            var ltiers = tiers.ToList();
            foreach (var tier in tiers)
            {
                //Debug.LogWarning("INIT: " + tier.name);
                var dtier = DepletedItemTier.New(tier);
                if (dtier != null)
                {
                    //Debug.LogWarning("ADD: " + dtier.name);
                    ltiers.Add(dtier);
                }
            }
            return tiers.ToArray();
        }


        public static ItemDef[] OnItemCatalogSetItemDefs(ItemDef[] items)
        {
            var litems = items.ToList();
            foreach (var item in items)
            {
                //Debug.LogWarning("ONSETDEF: " + item.name);
                var ditem = MakeDepletableItem(item);
                if (ItemAPI.Add(ditem))
                {
                    //Debug.LogWarning("ADD: " + ditem.ItemDef.name);
                    depletion.Add(item, ditem);
                    depleted.Add(ditem.ItemDef);
                    litems.Add(ditem.ItemDef);
                }
            }
            return litems.ToArray();
        }

        public static void OnGenerateRuntimeValues(ItemDisplayRuleSet rules)
        {
            var lassets = new List<ItemDisplayRuleSet.KeyAssetRuleGroup>();
            foreach (var g in rules.keyAssetRuleGroups)
                if (g.keyAsset is ItemDef item && depletion.TryGetValue(item, out var ditem))
                {
                    //Debug.LogWarning("UPDATEDDISPLAY: " + ditem.ItemDef.name);
                    lassets.Add(new ItemDisplayRuleSet.KeyAssetRuleGroup()
                        { keyAsset = ditem.ItemDef, displayRuleGroup = g.displayRuleGroup });
                }
            rules.keyAssetRuleGroups = rules.keyAssetRuleGroups.AddRangeToArray(lassets.ToArray());
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

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            var tier = DepletedItemTier.Get(item._itemTierDef);
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

            var itier = tier.tier;
            var tags = GenTags(item.tags);
            var name = "Depleted" + item.name;

            var ditem = new CustomItem(
                name, item.nameToken, item.descriptionToken, 
                item.loreToken, item.pickupToken, item.pickupIconSprite, 
                item.pickupModelPrefab, tags, itier, item.hidden, 
                item.canRemove, null, rules, tier);

            ItemCatalog.availability.CallWhenAvailable(() =>
            {
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
                ditem.ItemDef.tier = ditem.ItemDef._itemTierDef.tier;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
                ditem.ItemDef.pickupIconSprite = item.pickupIconSprite;
                ditem.ItemDef.pickupModelPrefab = item.pickupModelPrefab;
                ditem.ItemDef.requiredExpansion = item.requiredExpansion;
            });

            return ditem;
        }

    }
}
