using BattleTech;
using BattleTech.Data;
using BattleTech.UI;
using Harmony;
using HBS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
 */

namespace BetterSorting
{
    [HarmonyPatch(typeof(BattleTech.SalvageDef), "GetSalvageSortVal")]
    public static class Patch_BattleTech_SalvageDef_GetSalvageSortVal
    {
        // Stop original GetSalvageSortVal from running
        private static bool Prefix()
        {
            return false;
        }

        private static void Postfix(SalvageDef __instance, ref int __result)
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
                case ComponentType.MechPart:
                    __result = 100;

                    DataManager dm = LazySingletonBehavior<UnityGameInstance>.Instance.Game.DataManager;
                    if (dm.MechDefs.Exists(item.Description.Id))
                    {
                        MechDef mechDef = dm.MechDefs.Get(item.Description.Id);
                        __result += (int)mechDef.Chassis.Tonnage;
                    }
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
        private static bool Prefix()
        {
            return false;
        }

        private static void Postfix(ShopDefItem __instance, ref int __result)
        {
            DataManager dm = LazySingletonBehavior<UnityGameInstance>.Instance.Game.DataManager;
            ShopDefItem item = __instance;
            MechComponentDef ComponentDef = null;
            __result = 0;

            switch (item.Type)
            {
                case ShopItemType.Upgrade:
                    if (dm.UpgradeDefs.Exists(item.GUID))
                        ComponentDef = dm.UpgradeDefs.Get(item.GUID);
                    break;

                case ShopItemType.Weapon:
                    if (dm.WeaponDefs.Exists(item.GUID))
                        ComponentDef = dm.WeaponDefs.Get(item.GUID);
                    break;

                case ShopItemType.AmmunitionBox:
                    if (dm.AmmoBoxDefs.Exists(item.GUID))
                        ComponentDef = dm.AmmoBoxDefs.Get(item.GUID);
                    break;

                case ShopItemType.JumpJet:
                    if (dm.JumpJetDefs.Exists(item.GUID))
                        ComponentDef = dm.JumpJetDefs.Get(item.GUID);
                    break;

                case ShopItemType.HeatSink:
                    if (dm.HeatSinkDefs.Exists(item.GUID))
                        ComponentDef = dm.HeatSinkDefs.Get(item.GUID);
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
                    if (dm.ChassisDefs.Exists(id))
                    {
                        ChassisDef ChassisDef = dm.ChassisDefs.Get(id);
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

    [HarmonyPatch(typeof(MechLabInventoryWidget), "SetSortOptions", null)]
    public static class BattleTech_UI_MechLabInventoryWidget_SetSortOptions_Transpile
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // from the last to first instruction search for the call to [this.filterDropdown.value = 0] and instead set it to 1
            // which is the built-in sort by type function
            List<CodeInstruction> instList = instructions.ToList<CodeInstruction>();
            for (int idx = instList.Count - 1; idx >= 0; idx--)
            {
                CodeInstruction instruction = instList[idx];
                if (instruction.operand != null && !(instruction.opcode != OpCodes.Callvirt))
                {
                    if ((instruction.operand as MethodInfo)?.Name == "set_value")
                    {
                        instList[idx - 1].opcode = OpCodes.Ldc_I4_1;
                    }
                }
            }
            return instList.AsEnumerable();
        }
    }

    [HarmonyPatch(typeof(MechLabInventoryWidget), "SortBy_Type")]
    public static class BattleTech_UI_MechLabInventoryWidget_SortBy_Type_Prefix
    {
        // replace the unused and broken sortBy_Type with a more comprehensive comparison
        public static bool Prefix(MechLabInventoryWidget __instance, InventoryItemElement a, InventoryItemElement b, ref int __result)
        {
            try
            {
                int num = 0;
                char[] trimChars = new char[] { '+', ' ' };

                WeaponDef weaponDef = a?.controller?.weaponDef;
                WeaponDef weaponDef2 = b?.controller?.weaponDef;

                if (weaponDef != null && weaponDef2 != null)
                {
                    // category (missile, ballistic...)
                    num = weaponDef.Category.CompareTo(weaponDef2.Category);
                    // sub-type (SRM,LRM...)
                    if (num == 0)
                    {
                        num = weaponDef.WeaponSubType.CompareTo(weaponDef2.WeaponSubType);
                    }
                    // tonnage
                    if (num == 0)
                    {
                        num = weaponDef.Tonnage.CompareTo(weaponDef2.Tonnage);
                    }
                    // rarity (descending)
                    if (num == 0)
                    {
                        num = weaponDef2.Description.Rarity.CompareTo(weaponDef.Description.Rarity);
                    }
                    // manufacturer
                    if (num == 0)
                    {
                        num = weaponDef.Description.Manufacturer.CompareTo(weaponDef2.Description.Manufacturer);
                    }
                }
                else if (weaponDef != null)
                {
                    num = -1;
                }
                else if (weaponDef2 != null)
                {
                    num = 1;
                }
                else if (a?.controller?.ammoBoxDef != null && b?.controller?.ammoBoxDef != null) // ammo
                {
                    // ammo type
                    num = a.controller.ammoBoxDef.Ammo.Category.CompareTo(b.controller.ammoBoxDef.Ammo.Category);
                }
                else if (a?.controller?.componentDef != null && b?.controller?.componentDef != null) // equipment
                {
                    // remove + from ui name and compare
                    num = a.controller.componentDef.Description.UIName.Trim(trimChars).CompareTo(b.controller.componentDef.Description.UIName.Trim(trimChars));
                    // rarity (descending)
                    if (num == 0)
                    {
                        num = b.controller.componentDef.Description.Rarity.CompareTo(a.controller.componentDef.Description.Rarity);
                    }
                }
                else
                {
                    // general type
                    num = a.ComponentRef.ComponentDefType.CompareTo(b.ComponentRef.ComponentDefType);
                }

                __result = num;
                return false;
            }
            catch (Exception)
            {
                return true;
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