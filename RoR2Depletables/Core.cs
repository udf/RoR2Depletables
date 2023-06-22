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
            public static Dictionary<ItemTier, DepletedItemTier> cache = new Dictionary<ItemTier, DepletedItemTier>();

            public static ItemTier Index(ItemTierDef tier)
            {
                if (tier is null) return ItemTier.NoTier;
                ItemTier i;
                if (ItemTierCatalog.availability.available && tier.tier != ItemTier.AssignedAtRuntime) i = tier.tier;
#pragma warning disable CS0642 // Possible mistaken empty statement
                else if (Enum.TryParse(tier.name.Substring(0,tier.name.Length-3), out i));
                else if (Enum.TryParse(tier.name.Substring(0,tier.name.Length-7), out i));
#pragma warning restore CS0642 // Possible mistaken empty statement
                else i = tier.tier;
                if (i == ItemTier.AssignedAtRuntime)
                    i = ItemTier.NoTier;
                return i;
            }

            public static DepletedItemTier Get(ItemTierDef tier)
            {
                var i = Index(tier);
                return i != ItemTier.NoTier && cache.TryGetValue(i, out var d) ? d : null;
            }

            public static DepletedItemTier Get(ItemTier tier)
            {
                return tier != ItemTier.AssignedAtRuntime && tier != ItemTier.NoTier && cache.TryGetValue(tier, out var d) ? d : null;
            }

            public static DepletedItemTier New(ItemTierDef tier)
            {

                var i = Index(tier);
                return i == ItemTier.NoTier || cache.ContainsKey(i) ? null : CreateInstance<DepletedItemTier>().Init(tier, i);
            }

            public DepletedItemTier Init(ItemTierDef tier, ItemTier i)
            {
                cache.Add(i, this);
                this.tier = i;

                if (i == ItemTier.NoTier)
                    tier.name = Enum.GetName(typeof(ItemTier),i);

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
                Debug.LogWarning("INIT: " + tier.name);
                var dtier = DepletedItemTier.New(tier);
                if (dtier != null)
                {
                    Debug.LogWarning("ADD: " + dtier.name);
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
                Debug.LogWarning("ONSETDEF: " + item.name);
                var ditem = MakeDepletableItem(item);
                if (ItemAPI.Add(ditem))
                {
                    Debug.LogWarning("ADD: " + ditem.ItemDef.name);
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
                    Debug.LogWarning("UPDATEDDISPLAY: " + ditem.ItemDef.name);
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
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            ItemTierDef tier = DepletedItemTier.Get(item.tier);
            if ((tier?.tier ?? ItemTier.NoTier) == ItemTier.NoTier)
                tier = DepletedItemTier.Get(item._itemTierDef);
            if ((tier?.tier ?? ItemTier.NoTier) == ItemTier.NoTier)
                tier = item._itemTierDef;
            if ((tier?.tier ?? ItemTier.NoTier) == ItemTier.NoTier)
                tier = null;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public

            var itier = tier?.tier ?? item.tier;
            if (itier == ItemTier.AssignedAtRuntime)
            { 
                itier = ItemTier.NoTier;
                tier = null;
            }
            else if (tier != null && itier != ItemTier.NoTier)
                tier.tier = itier;

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
                ditem.ItemDef.tier = ditem.ItemDef._itemTierDef?.tier ?? ditem.ItemDef.tier;
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
                ditem.ItemDef.pickupIconSprite = item.pickupIconSprite;
                ditem.ItemDef.pickupModelPrefab = item.pickupModelPrefab;
                ditem.ItemDef.requiredExpansion = item.requiredExpansion;
            });

            return ditem;
        }

    }
}
