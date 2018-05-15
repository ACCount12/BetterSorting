using System.Reflection;
using Harmony;
using BattleTech;
using BattleTech.Data;
using HBS;

/*
 * Sorting order: (priority)
 * 1. LosTech (500)
 * 2. Mechs (300 + tonnage)
 * 3. Mech parts (100 + tonnage)
 * 4. Upgrades (9)
 * 5. Rare "heat sinks": heat banks, etc (8)
 * 6. Improved weapons (5)
 * 7. Generic weapons (4)
 * 8. Generic ammo (3)
 * 9. Generic jump jets (2)
 * 10. Generic heat sinks (1)
 * 
 * I have no idea how to get mech tonnage on SalvageDefs, so those are not sorted by tonnage yet.
 * 
 */

namespace BetterSorting
{
    [HarmonyPatch(typeof(BattleTech.SalvageDef), "GetSalvageSortVal")]
    public static class Patch_BattleTech_SalvageDef_GetSalvageSortVal
    {
        // Stop original GetSalvageSortVal from running
        static bool Prefix()
        {
            return false;
        }

        static void Postfix(SalvageDef __instance, ref int __result)
        {
            SalvageDef item = __instance;
            MechComponentDef ComponentDef = item.MechComponentDef;
            __result = 0;

            // LosTech > all
            if (ComponentDef != null && ComponentDef.ComponentTags.Contains("component_type_lostech"))
            {
                __result = 500;
                return;
            }


            switch (item.ComponentType)
            {
                // No idea on how to sort by mech tonnage here.
                // Feel free to help me out.
                case ComponentType.MechPart:
                    __result = 100;
                    break;

                case ComponentType.Upgrade:
                    __result = 9;
                    break;

                case ComponentType.Weapon:
                    if (ComponentDef != null && (!string.IsNullOrEmpty(ComponentDef.BonusValueA) || !string.IsNullOrEmpty(ComponentDef.BonusValueB)))
                        __result = 5;
                    else
                        __result = 4;
                    break;

                case ComponentType.AmmunitionBox:
                    __result = 3;
                    break;
                
                case ComponentType.JumpJet:
                    __result = 2;
                    break;

                case ComponentType.HeatSink:
                    if (ComponentDef != null && ComponentDef.Description.Rarity > 0)
                        __result = 8;
                    else
                        __result = 1;
                    break;
            }
        }

    }


    [HarmonyPatch(typeof(BattleTech.ShopDefItem), "GetShopDefSortVal")]
    public static class Patch_BattleTech_ShopDefItem_GetShopDefSortVal
    {
        // Stop original GetShopDefSortVal from running
        static bool Prefix()
        {
            return false;
        }

        static void Postfix(ShopDefItem __instance, ref int __result)
        {
            DataManager dataManager = LazySingletonBehavior<UnityGameInstance>.Instance.Game.DataManager;
            ShopDefItem item = __instance;
            MechComponentDef ComponentDef = null;
            __result = 0;

            switch (item.Type)
            {
                case ShopItemType.Upgrade:
                    if (dataManager.UpgradeDefs.Exists(item.GUID))
                        ComponentDef = dataManager.UpgradeDefs.Get(item.GUID);
                    break;

                case ShopItemType.Weapon:
                    if (dataManager.WeaponDefs.Exists(item.GUID))
                        ComponentDef = dataManager.WeaponDefs.Get(item.GUID);
                    break;

                case ShopItemType.AmmunitionBox:
                    if (dataManager.AmmoBoxDefs.Exists(item.GUID))
                        ComponentDef = dataManager.AmmoBoxDefs.Get(item.GUID);
                    break;
                        
                case ShopItemType.JumpJet:
                    if (dataManager.JumpJetDefs.Exists(item.GUID))
                        ComponentDef = dataManager.JumpJetDefs.Get(item.GUID);
                    break;

                case ShopItemType.HeatSink:
                    if (dataManager.HeatSinkDefs.Exists(item.GUID))
                        ComponentDef = dataManager.HeatSinkDefs.Get(item.GUID);
                    break;
            }


            // LosTech > all
            if (ComponentDef != null && ComponentDef.ComponentTags.Contains("component_type_lostech"))
            {
                __result = 500;
                return;
            }


            switch (item.Type)
            {
                case ShopItemType.Mech:
                case ShopItemType.MechPart:

                    if (item.Type == ShopItemType.Mech)
                        __result = 300;
                    else
                        __result = 100;

                    string id = item.GUID.Replace("mechdef", "chassisdef");
                    if (dataManager.ChassisDefs.Exists(id))
                    {
                        ChassisDef ChassisDef = dataManager.ChassisDefs.Get(id);
                        if (ChassisDef != null)
                            __result += (int)ChassisDef.Tonnage;
                    }
                    break;

                case ShopItemType.Upgrade:
                    __result = 9;
                    break;

                case ShopItemType.Weapon:
                    if (ComponentDef != null && (!string.IsNullOrEmpty(ComponentDef.BonusValueA) || !string.IsNullOrEmpty(ComponentDef.BonusValueB)))
                        __result = 5;
                    else
                        __result = 4;
                    break;

                case ShopItemType.AmmunitionBox:
                    __result = 3;
                    break;

                case ShopItemType.JumpJet:
                    __result = 2;
                    break;

                case ShopItemType.HeatSink:
                    if (ComponentDef != null && ComponentDef.Description.Rarity > 0)
                        __result = 8;
                    else
                        __result = 1;
                    break;
            }
        }

    }

    public static class BetterSorting
    {
        public static void Init()
        {
            var harmony = HarmonyInstance.Create("org.null.ACCount.BetterSorting");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
