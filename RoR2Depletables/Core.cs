using RoR2;
using R2API;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using static RoR2Depletables.Main;

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
namespace RoR2Depletables
{
    public static class Core
    {
        public static HashSet<string> excludedItems = new HashSet<string>();

        public static List<ItemTag> exceptTags = new List<ItemTag> {};
        public static List<ItemTag> concatTags = new List<ItemTag> {ItemTag.Cleansable,ItemTag.AIBlacklist,ItemTag.WorldUnique};

        public static string customTagName = "Depleted";
        public static ItemTag? customTag = null;

        public static Dictionary<ItemDef, ItemDef> depletion = new Dictionary<ItemDef, ItemDef>();
        public static HashSet<ItemDef> depleted = new HashSet<ItemDef>();
        public static Dictionary<string, ItemDef> depletedTokens = new Dictionary<string, ItemDef>();

        public static string suffixA = "Depleted";
        public static string suffixB = "_DEPLETED";

        public static List<Action> delayedLanguage = new List<Action>();
        public static string extraDescription = " <style=cIsUtility>Cannot be <style=cIsVoid>corrupted</style></style>.";

        public static bool doOriginalItemCount = false;

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

                colorIndex = tier.colorIndex;
                darkColorIndex =  tier.darkColorIndex;
                highlightPrefab = tier.highlightPrefab;
                dropletDisplayPrefab = tier.dropletDisplayPrefab;
                bgIconTexture = tier.bgIconTexture;

                ItemCatalog.availability.CallWhenAvailable(() => {
                    if (tier.bgIconTexture != null && configIconColours.Value && configShowLogbook.Value)
                        bgIconTexture = Stain(tier.bgIconTexture, configDepletedColour.Value);
                });

                return this;
            }
        }

        public static ItemTag[] GenTags(ItemTag[] tags)
        {
            return tags.Except(exceptTags).Concat(concatTags).Distinct().ToArray();
        }

        public static ItemTierDef[] OnItemTierCatalogInit(ItemTierDef[] tiers)
        {
            customTag = ItemAPI.AddItemTag(customTagName);
            concatTags.Add(customTag.Value);
            var ltiers = tiers.ToList();
            foreach (var tier in tiers)
            {
                var dtier = DepletedItemTier.New(tier);
                if (dtier != null)
                    ltiers.Add(dtier);
            }
            return tiers.ToArray();
        }


        public static ItemDef[] OnItemCatalogSetItemDefs(ItemDef[] items)
        {
            var litems = items.ToList();
            foreach (var item in items)
            {
                var ditem = MakeDepletableItem(item);
                if (ditem != null && ItemAPI.Add(ditem))
                    litems.Add(ditem.ItemDef);
            }
            return litems.ToArray();
        }

        public static void LateOnItemCatalogSetItemDefs(ItemDef[] items)
        {
            // this is so other mods can further derive items and it might still work
            // so long as they kept the tag intact and it's possible to find the base item

            var ditems = items.Where(i => !depleted.Contains(i) && i.ContainsTag(customTag.Value));
            var nitems = items.Where(i => i.DoesNotContainTag(customTag.Value)).ToList();

            ItemDef item;
            foreach (var ditem in ditems)
            {
                // assume derived items will leave the lore unchanged
                var loreMatched = nitems.Where(i => i.loreToken == ditem.loreToken).ToList();
                if (loreMatched.Count == 0) continue;
                if (loreMatched.Count == 1) item = loreMatched.First();
                else
                {
                    // there may be multiple derived items, maybe they are arranged by tier?
                    var tierMatched = loreMatched.Where(i => i.tier == ditem.tier).ToList();
                    if (tierMatched.Count == 0) 
                        item = loreMatched.First();
                    item = tierMatched.First();
                }

                nitems.Remove(item);
                LateMakeDepletableItem(ditem, item);
            }
        }

        public static void OnGenerateRuntimeValues(ItemDisplayRuleSet rules)
        {
            var lassets = new List<ItemDisplayRuleSet.KeyAssetRuleGroup>();
            foreach (var g in rules.keyAssetRuleGroups)
                if (g.keyAsset is ItemDef item && depletion.TryGetValue(item, out var ditem))
                    lassets.Add(new ItemDisplayRuleSet.KeyAssetRuleGroup()
                        { keyAsset = ditem, displayRuleGroup = g.displayRuleGroup });
            rules.keyAssetRuleGroups = rules.keyAssetRuleGroups.AddRangeToArray(lassets.ToArray());
        }

        public static void OnContagiousItemManagerStepInventoryInfection(Inventory inventory, ItemIndex original, int limit)
        {
            var item = ItemCatalog.GetItemDef(original);
            if (depletion.TryGetValue(item,out var ditem))
            {
                doOriginalItemCount = true;
                var count = inventory.GetItemCount(item);
                doOriginalItemCount = false;
                inventory.GiveItem(ditem, Math.Min(count,limit));
            }
        }

        public static Color Border(Color color)
        {
            Color.RGBToHSV(color.NoAlpha(), out var h, out var s, out var v);
            return Color.HSVToRGB(h,s*1.2f,v*0.9f);
        }

        public static Color Border(Color colorA, Color colorB)
        {
            Color.RGBToHSV(colorA.NoAlpha(), out var hA, out var sA, out var vA);
            Color.RGBToHSV(colorB.NoAlpha(), out var hB, out var sB, out var vB);
            return Color.HSVToRGB((hA+hB)/2,(sA+sB)*0.6f,(vA+vB)*0.45f);
        }

        public static Texture2D Stain(Texture texture, Color stain)
        {
            Color? aura = null;
            return texture.Duplicate((x,y,c) => {
                if (aura is null) aura = Border(c);
                var a = aura.Value;
                var d = c-a;
                var m = d.r*d.r + d.g*d.g + d.b*d.b;
                var s = (float)Math.Abs(Math.Tanh(4*m));
                return Color.Lerp(stain,c,(1+2*s)/3).AlphaMultiplied(c.a);
            });
        }

        public static CustomItem MakeDepletableItem(ItemDef item, ItemDisplayRule[] rules = null)
        {
            if (item.hidden) return null;
            if (item.tier == ItemTier.NoTier) return null;
            if (item.tier == ItemTier.AssignedAtRuntime) return null;

            if (!configMakeDepleted.Value) return null;

            var lname = Language.GetString(item.nameToken, configLanguage.Value);
            var exclude = excludedItems.Contains(item.name.ToLower())
                || excludedItems.Contains(item.nameToken.ToLower())
                || excludedItems.Contains(lname.ToLower());
            if (exclude != configInvertBlacklist.Value) return null;

            ItemTierDef tier = DepletedItemTier.Get(item.tier);
            var itier = tier?._tier ?? item.tier;

            var tags = GenTags(item.tags);
            var token = item.nameToken + suffixB;

            var ditem = new CustomItem(
                item.name + suffixA, token, item.descriptionToken + suffixB,
                item.loreToken, item.pickupToken + suffixB, item.pickupIconSprite, 
                item.pickupModelPrefab, tags, itier, false, 
                false, item.unlockableDef, rules, tier);

            LateMakeDepletableItem(ditem.ItemDef, item);
            return ditem;
        }

        public static void RegisterDepletableItem(ItemDef ditem, ItemDef item)
        {
            depletion[item] = ditem;
            depleted.Add(ditem);
            depletedTokens[ditem.nameToken] = ditem;
        }

        public static void LateMakeDepletableItem(ItemDef ditem, ItemDef item)
        {
            RegisterDepletableItem(ditem, item);

            ItemCatalog.availability.CallWhenAvailable(() =>
            {
                ditem.tier = ditem._itemTierDef?._tier ?? ditem.tier;
                ditem.requiredExpansion = item.requiredExpansion;

                var sprite = item.pickupIconSprite;

                if (configIconColours.Value) {
                    var texture = Stain(sprite.texture, configDepletedColour.Value);
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                }

                ditem.pickupIconSprite = sprite;
                ditem.pickupModelPrefab = item.pickupModelPrefab;
            });

            delayedLanguage.Add(() => {
                LanguageAPI.AddOverlay(ditem.nameToken, "Voidtouched " + Language.GetString(item.nameToken, configLanguage.Value));
                LanguageAPI.AddOverlay(ditem.pickupToken, Language.GetString(item.pickupToken) + extraDescription);
                LanguageAPI.AddOverlay(ditem.descriptionToken, Language.GetString(item.descriptionToken) + extraDescription);
            });
        }

    }
}
#pragma warning restore Publicizer001 // Accessing a member that was not originally public