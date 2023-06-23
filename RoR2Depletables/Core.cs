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

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
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
#pragma warning disable CS0642 // Possible mistaken empty statement
                if (Enum.TryParse(tier.name.Substring(0,tier.name.Length-3), out i));
                else if (Enum.TryParse(tier.name.Substring(0,tier.name.Length-7), out i));
                else i = tier._tier;
#pragma warning restore CS0642 // Possible mistaken empty statement
                if (i == ItemTier.AssignedAtRuntime)
                    i = ItemTier.NoTier;
                return i;
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
                _tier = i;

                name = suffixA + tier.name;

                isDroppable = false;
                canScrap = false;
                canRestack = false;
                pickupRules = PickupRules.Default;

                colorIndex = ColorCatalog.ColorIndex.VoidItem;//tier.colorIndex;
                darkColorIndex =  ColorCatalog.ColorIndex.VoidItemDark;//tier.darkColorIndex;
                bgIconTexture = tier.bgIconTexture;//Stain(tier.bgIconTexture);
                highlightPrefab = tier.highlightPrefab;
                dropletDisplayPrefab = tier.dropletDisplayPrefab;

                return this;
            }
        }

        public static List<ItemTag> exceptTags = new List<ItemTag> {ItemTag.Scrap, ItemTag.PriorityScrap};
        public static List<ItemTag> concatTags = new List<ItemTag> {ItemTag.Cleansable,ItemTag.AIBlacklist,ItemTag.WorldUnique};

        public static string customTagName = "Depleted";
        public static ItemTag? customTag = null;

        public static ItemTag[] GenTags(ItemTag[] tags)
        {
            if (customTag is null)
            {
                customTag = ItemAPI.AddItemTag(customTagName);
                concatTags.Add(customTag.Value);
            }
            return tags.Except(exceptTags).Concat(concatTags).Distinct().ToArray();
        }

        public static Dictionary<ItemDef, CustomItem> depletion = new Dictionary<ItemDef, CustomItem>();
        public static HashSet<ItemDef> depleted = new HashSet<ItemDef>();
        public static Dictionary<string, ItemDef> depletedTokens = new Dictionary<string, ItemDef>();

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
                if (ditem != null && ItemAPI.Add(ditem))
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
                //Debug.LogWarning("CONVERT: " + ditem.ItemDef.name);
                doOriginalItemCount = true;
                var count = inventory.GetItemCount(item);
                doOriginalItemCount = false;
                inventory.GiveItem(ditem.ItemDef, Math.Min(count,limit));
            }
        }

        public static string suffixA = "Depleted";
        public static string suffixB = "_DEPLETED";

        public static Color stain = new Color(0.4f,0.1f,0.7f);
        public static Texture2D Stain(Texture texture)
        {
            Color? aura = null;
            return texture.Duplicate((x,y,c) => {
                if (aura is null) aura = c.gamma;
                var a = aura.Value;
                var r = c.gamma;
                var d = (r-a).grayscale;
                var s = (float)Math.Tanh(Math.Pow(d/0.1,3));
                r = r.RGBMultiplied(Color.Lerp(stain,Color.white,c.grayscale));
                return Color.Lerp(c,r,Math.Abs(s)).linear.AlphaMultiplied(c.a);
            });

        }

        public static CustomItem MakeDepletableItem(ItemDef item, ItemDisplayRule[] rules = null)
        {
            if (item.hidden) return null;
            if (item.tier == ItemTier.NoTier) return null;
            if (item.tier == ItemTier.AssignedAtRuntime) return null;

            ItemTierDef tier = DepletedItemTier.Get(item.tier);
            var itier = tier?._tier ?? item.tier;

            //Debug.LogWarning(String.Join(", ", item.tags));
            var tags = GenTags(item.tags);
            var name = item.name + suffixA;
            var token = item.nameToken + suffixB;
            var descr = item.pickupToken + suffixB;

            var ditem = new CustomItem(
                name, token, null, 
                null, descr, item.pickupIconSprite, 
                item.pickupModelPrefab, tags, itier, false, 
                false, null, rules, tier);

            depletedTokens.Add(token,ditem.ItemDef);

            ItemCatalog.availability.CallWhenAvailable(() =>
            {
                ditem.ItemDef.tier = ditem.ItemDef._itemTierDef?._tier ?? ditem.ItemDef.tier;
                ditem.ItemDef.requiredExpansion = item.requiredExpansion;
                ditem.ItemDef.pickupToken = Language.GetString(item.pickupToken)
                    + " <style=cIsUtility>Cannot be <style=cIsVoid>corrupted</style></style>.";

                var sprite = item.pickupIconSprite;
                var texture = Stain(sprite.texture);
                sprite = Sprite.CreateSprite(texture,sprite.textureRect, sprite.pivot,
                sprite.pixelsPerUnit, 0, SpriteMeshType.Tight, sprite.border, false);
                
                ditem.ItemDef.pickupIconSprite = sprite;
                ditem.ItemDef.pickupModelPrefab = item.pickupModelPrefab;
            });

            return ditem;
        }

    }
}
#pragma warning restore Publicizer001 // Accessing a member that was not originally public