using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BetterSorting
{

    [HarmonyPatch(typeof(BattleTech.UI.MechLabInventoryWidget_ListView), "ApplySorting")]
    public static class BattleTech_UI_MechLabInventoryWidget_ListView_Patch
    {
        private static bool Prefix(MechLabInventoryWidget_ListView __instance)
        {
            IComparer<ListElementController_BASE> currComp = Traverse.Create(__instance).Field("currentListItemSorter").GetValue<IComparer<ListElementController_BASE>>();
            bool currDir = Traverse.Create(__instance).Field("invertSort").GetValue<bool>();

            try
            {
                Traverse.Create(__instance).Field("currentListItemSorter").SetValue(new Comparer_ListView());
                Traverse.Create(__instance).Field("invertSort").SetValue(true);
                return true;
            }
            catch (Exception e)
            {
                BetterSorting.Log($"Failed to sort MechLabInventoryWidget, resetting to default.{Environment.NewLine}Details:{e.ToString()}");
                Traverse.Create(__instance).Field("currentSort").SetValue(currComp);
                Traverse.Create(__instance).Field("invertSort").SetValue(currDir);
                return true;
            }
        }

        public class Comparer_ListView : IComparer<ListElementController_BASE>
        {
            public int Compare(ListElementController_BASE a, ListElementController_BASE b)
            {
                return BetterSorting.Compare(ListElementControllerCompare.Wrap(b), ListElementControllerCompare.Wrap(a));
            }
        }
    }

    [HarmonyPatch(typeof(BattleTech.UI.MechLabInventoryWidget), "ApplySorting")]
    public static class BattleTech_UI_MechLabInventoryWidget_Patch
    {
        private static bool Prefix(MechLabInventoryWidget __instance)
        {

            Comparison<InventoryItemElement_NotListView> currComp = Traverse.Create(__instance).Field("currentSort").GetValue<Comparison<InventoryItemElement_NotListView>>();
            bool currDir = Traverse.Create(__instance).Field("invertSort").GetValue<bool>();

            try
            {
                Traverse.Create(__instance).Field("currentSort").SetValue(new Comparison<InventoryItemElement_NotListView>(Compare_NotListView));
                Traverse.Create(__instance).Field("invertSort").SetValue(true);
                return true;
            }
            catch (Exception e)
            {
                BetterSorting.Log($"Failed to sort MechLabInventoryWidget, resetting to default.{Environment.NewLine}Details:{e.ToString()}");
                Traverse.Create(__instance).Field("currentSort").SetValue(currComp);
                Traverse.Create(__instance).Field("invertSort").SetValue(currDir);
                return true;
            }
        }

        public static int Compare_NotListView(InventoryItemElement_NotListView a, InventoryItemElement_NotListView b)
        {
            return BetterSorting.Compare(ListElementControllerCompare.Wrap(b.controller), ListElementControllerCompare.Wrap(a.controller));
        }

    }

    public static class BetterSorting
    {
        public static Dictionary<string, double> m_mfrCache = new Dictionary<string, double>();
        public static int m_maxWeapCat;

        public static void Init()
        {
            var harmony = HarmonyInstance.Create("org.null.ACCount.BetterSorting");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            m_maxWeapCat = (int)Enum.GetValues(typeof(WeaponCategory)).Cast<WeaponCategory>().Max();
        }

        private static ILog m_log = HBS.Logging.Logger.GetLogger(typeof(BetterSorting).Name, LogLevel.Log);

        public static void Log(string message)
        {
            //m_log.Log(message);
        }

        /*
        * Sorting order: (priority)
        * 1. LosTech 500.000+
        * 2. Mechs 400.000+
        * 3. Mech parts 350.000+
        * 4. Upgrades 250.000+
        * 5. Rare "heat sinks": heat banks, etc 225.000+
        * 6. Weapons 10.000->~160.000 (store/salvage has rare items at the top)
        * 7. Generic ammo 5.000+
        * 8. Generic other equipment ~500
        *
        */
        private static readonly char[] trimChars = new char[] { '+', ' ' };

        public static int Compare(ListElementControllerCompare a, ListElementControllerCompare b)
        {
            // in general we can rely on the custom comparer for sorting, with a few exceptions handled below
            int num = BetterSorting.GetSortVal(a).CompareTo(BetterSorting.GetSortVal(b));

            // upgrades don't have a sub-type to identify them, so a name comparison is necessary to sort them together
            if (num == 0 && a?.componentDef != null && b?.componentDef != null && a.componentDef.ComponentType == ComponentType.Upgrade)
            {
                Log($"comparing equipment by name...");
                // normalize name and compare (ASC)
                num = b.componentDef.Description.UIName.Trim(trimChars).CompareTo(a.componentDef.Description.UIName.Trim(trimChars));
                if (num == 0)
                {
                    // rarity
                    num = a.componentDef.Description.Rarity.CompareTo(b.componentDef.Description.Rarity);
                }
            }

            return num;
        }

        public static double GetSortVal(ListElementControllerCompare item)
        {
            try
            {
                double num = 0;
                bool in_mechLab = (item?.shopDefItem == null && item?.salvageDef == null);

                if (item?.chassisDef != null)
                {
                    num = (int)item?.chassisDef.Tonnage;
                    if ((item?.shopDefItem != null && item.shopDefItem.Type == ShopItemType.Mech) || (item?.salvageDef != null && item?.salvageDef.Type == SalvageDef.SalvageType.CHASSIS))
                    {
                        Log("found mech.");
                        num += 400000;
                    }
                    else if ((item?.shopDefItem != null && item.shopDefItem.Type == ShopItemType.MechPart) || (item?.salvageDef != null && item?.salvageDef.Type == SalvageDef.SalvageType.MECH_PART))
                    {
                        Log("found mech part.");
                        num += 350000;
                    }
                }

                // weapons (10000 - ~60000) shop: 10000 - ~160000
                if (item?.weaponDef != null)
                {
                    WeaponDef wd = item.weaponDef;
                    Log($"found weapon: {wd.Description.Id}");
                    // category (inverted, AC before LASER, etc.)
                    num += (m_maxWeapCat - (int)wd.Category) * 10000;
                    // sub-type (SRM,LRM...)
                    num += (int)wd.WeaponSubType * 100;
                    // tonnage
                    num += wd.Tonnage;
                    // quality
                    num += wd.Description.UIName.Split('+').Length - 1;

                    // in the store/salvage place rare items before all other weapons
                    if (wd.Description.Rarity > 0 && !in_mechLab)
                    {
                        num += 100000;
                    }
                }
                else if (item?.ammoBoxDef != null)
                {
                    // ammo
                    if (item.ammoBoxDef != null)
                    {
                        Log("found ammo.");
                        num += 5000 + (int)item.ammoBoxDef.Ammo.Category;
                    }
                }
                else if (item?.componentDef != null)
                {
                    Log($"found upgrade/equipment: {item.componentDef.Description.Id}");

                    if (!in_mechLab)
                    {
                        // upgrades/equipment
                        if (item.componentDef.ComponentType == ComponentType.Upgrade)
                            num += 250000;

                        // improved heatsink
                        if (item.componentDef.ComponentType == ComponentType.HeatSink && item.componentDef.Description.Rarity > 0)
                            num += 225000;
                    }

                    // component type
                    num += (int)item.componentDef.ComponentType * 100;
                    // sub type
                    //num += (int)item.componentDef.ComponentSubType * 10;
                }

                // lostech
                if (item.IsLosTech() && !in_mechLab)
                {
                    Log("  -adjusting for lostech");
                    num += 500000;
                }

                Log($"  sort val:{num}");
                return num;

            }
            catch (Exception e)
            {
                Log($"Failed to get sortVal for {item.GetType().ToString()}: {e.ToString()}");
                return 0;
            }
        }
    }

    public class ListElementControllerCompare
    {
        public SalvageDef salvageDef;

        public MechComponentDef componentDef;

        public AmmunitionBoxDef ammoBoxDef;

        public WeaponDef weaponDef;

        public MechDef mechDef;

        public ChassisDef chassisDef;

        public ShopDefItem shopDefItem;

        private static string LOSTECH = "component_type_lostech";

        public static ListElementControllerCompare Wrap(ListElementController_BASE item)
        {
            return new ListElementControllerCompare(item);
        }

        public static ListElementControllerCompare Wrap(ListElementController_BASE_NotListView item)
        {
            return new ListElementControllerCompare(item);
        }

        private ListElementControllerCompare(ListElementController_BASE item)
        {
            salvageDef = item.salvageDef;
            componentDef = item.componentDef;
            ammoBoxDef = item.ammoBoxDef;
            weaponDef = item.weaponDef;
            mechDef = item.mechDef;
            chassisDef = item.chassisDef;
            shopDefItem = item.shopDefItem;
        }

        private ListElementControllerCompare(ListElementController_BASE_NotListView item)
        {
            salvageDef = item.salvageDef;
            componentDef = item.componentDef;
            ammoBoxDef = item.ammoBoxDef;
            weaponDef = item.weaponDef;
            mechDef = item.mechDef;
            chassisDef = item.chassisDef;
            shopDefItem = item.shopDefItem;
        }

        public bool IsLosTech()
        {
            return (
               (this?.componentDef?.ComponentTags != null && this.componentDef.ComponentTags.Contains(LOSTECH)) ||
               (ammoBoxDef?.ComponentTags != null && ammoBoxDef.ComponentTags.Contains(LOSTECH)) ||
               (weaponDef?.ComponentTags != null && weaponDef.ComponentTags.Contains(LOSTECH))
                );
        }

    }
}