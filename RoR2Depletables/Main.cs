using BepInEx;
using RoR2;
using RoR2.Items;
using R2API;
using R2API.Utils;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.SceneManagement;
using static RoR2Depletables.Utils;
using static RoR2Depletables.Core;
using Newtonsoft.Json.Utilities;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]
#pragma warning restore CS0618 // Type or member is obsolete

namespace RoR2Depletables
{
    [BepInPlugin("local.RoR2Depletables", "RoR2 Depletables", "0.0.0")]
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
                    if (depletedTokens.TryGetValue(entry.nameToken,out var ditem))
                    {
                        ditem.nameToken = "Voidtouched " + Language.GetString(entry.nameToken
                            .Substring(0,entry.nameToken.Length-suffixB.Length));
                    }
                    else entries.Add(entry);
                return entries.ToArray();
            };

            On.RoR2.Items.ContagiousItemManager.StepInventoryInfection += (orig, inv, item, limit, forced) =>
            {
                if (depleted.Contains(ItemCatalog.GetItemDef(item))) return false;
                OnContagiousItemManagerStepInventoryInfection(inv, item, limit);
                return orig.Invoke(inv, item, limit, forced);
            };

            On.RoR2.Inventory.GetItemCount_ItemDef += (orig, inv, item) =>
            {
                if (!doOriginalItemCount && item != null && depletion.TryGetValue(item, out var ditem))
                    return orig.Invoke(inv, item) + orig.Invoke(inv, ditem.ItemDef);
                return orig.Invoke(inv, item);
            };
        }

    }
}
