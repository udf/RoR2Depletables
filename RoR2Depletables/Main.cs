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
            On.RoR2.ItemCatalog.SetItemDefs += (orig, items) =>
            {
                OnItemCatalogSetItemDefs(items);
                orig.Invoke(items);
            };

            On.RoR2.ItemDisplayRuleSet.GenerateRuntimeValues += (orig,rules) =>
            {
                orig.Invoke(rules);
                OnGenerateRuntimeValues(rules);
            };

            On.RoR2.Items.ContagiousItemManager.StepInventoryInfection += (orig, inv, item, limit, forced) =>
            {
                if (depleted.Contains(ItemCatalog.GetItemDef(item))) return false;
                OnContagiousItemManagerStepInventoryInfection(inv, item, limit);
                return orig.Invoke(inv, item, limit, forced);
            };

            //On.RoR2.Items.ContagiousItemManager.ProcessPendingChanges += (orig) =>
            //{
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            //    ContagiousItemManager.pendingChanges = ContagiousItemManager.pendingChanges
            //        .Where(c => !depleted.Contains(ItemCatalog.GetItemDef(c.originalItem))).ToList();
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
            //    orig.Invoke();
            //};

            On.RoR2.Inventory.GetItemCount_ItemDef += (orig, inv, item) =>
            {
                if (!doOriginalItemCount && item != null && depletion.TryGetValue(item, out var ditem))
                    return orig.Invoke(inv, item) + orig.Invoke(inv, ditem.ItemDef);
                return orig.Invoke(inv, item);
            };
        }

    }
}
