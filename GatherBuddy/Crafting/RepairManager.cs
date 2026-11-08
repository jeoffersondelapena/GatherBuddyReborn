using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using GatherBuddy.Automation;
using GatherBuddy.ForkLogic;
using GatherBuddy.Utilities;

namespace GatherBuddy.Crafting;

public static unsafe class RepairManager
{
    public static int GetMinEquippedPercent()
    {
        ushort ret = ushort.MaxValue;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                if (item->Condition < ret)
                    ret = item->Condition;
            }
        }
        return (int)Math.Ceiling((double)ret / 300);
    }

    public static bool CanRepairAny(int repairPercent = 0)
    {
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                if (CanRepairItem(item->ItemId) && item->Condition / 300 < (repairPercent > 0 ? repairPercent : 100))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static bool CanRepairItem(uint itemId)
    {
        var itemSheet = Dalamud.GameData.Excel.GetSheet<Item>();
        if (itemSheet == null || !itemSheet.TryGetRow(itemId, out var item))
            return false;

        if (item.ClassJobRepair.RowId > 0)
        {
            var repairItem = item.ItemRepair.Value.Item;

            if (!HasDarkMatterOrBetter(repairItem.RowId))
                return false;

            var jobLevel = PlayerState.Instance()->ClassJobLevels[item.ClassJobRepair.Value.ExpArrayIndex];
            if (Math.Max(item.LevelEquip - 10, 1) <= jobLevel)
                return true;
        }

        return false;
    }

    public static bool HasDarkMatterOrBetter(uint darkMatterID)
    {
        var repairResources = Dalamud.GameData.Excel.GetSheet<ItemRepairResource>();
        if (repairResources == null)
            return false;

        foreach (var dm in repairResources)
        {
            if (dm.Item.RowId < darkMatterID)
                continue;

            if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.RowId) > 0)
                return true;
        }
        return false;
    }

    // fork: what each worn piece takes to repair by hand, judged in one go by RepairRules.SelfRepairBlocker
    public sealed class SelfRepairCheck
    {
        public readonly List<RepairRules.Need>  Needs  = new();
        public readonly Dictionary<string, int> Levels = new();

        public string? Blocker(int threshold)
            => RepairRules.SelfRepairBlocker(Needs, DarkMatterOnHand(), Levels, threshold);

        public void Add(InventoryItem* slot, string name)
        {
            var items = Dalamud.GameData.Excel.GetSheet<Item>();
            if (slot == null || slot->ItemId == 0 || !items.TryGetRow(slot->ItemId, out var item) || item.ClassJobRepair.RowId == 0)
                return;

            var job        = item.ClassJobRepair.Value;
            var darkMatter = item.ItemRepair.Value.Item;
            var repairer   = TextRules.Capitalized(job.Name.ExtractText());
            Levels[repairer] = PlayerState.Instance()->ClassJobLevels[job.ExpArrayIndex];
            Needs.Add(new RepairRules.Need(name, Grade(darkMatter.RowId), darkMatter.Value.Name.ExtractText(), repairer,
                Math.Max(1, item.LevelEquip - 10)));
        }
    }

    public static SelfRepairCheck EquippedCheck(Func<ushort, bool> worn)
    {
        var check     = new SelfRepairCheck();
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0 && worn(item->Condition))
                check.Add(item, $"the {RepairRules.SlotName(i)}");
        }

        return check;
    }

    // dark matter grade -> units in the bags
    public static Dictionary<int, int> DarkMatterOnHand()
    {
        var stock = new Dictionary<int, int>();
        foreach (var grade in Dalamud.GameData.Excel.GetSheet<ItemRepairResource>())
        {
            var count = grade.Item.RowId == 0 ? 0 : InventoryManager.Instance()->GetInventoryItemCount(grade.Item.RowId);
            if (count > 0)
                stock[(int)grade.RowId] = count;
        }

        return stock;
    }

    // the ItemRepairResource row of a dark matter item is its grade
    private static int Grade(uint darkMatterItemId)
    {
        foreach (var grade in Dalamud.GameData.Excel.GetSheet<ItemRepairResource>())
            if (grade.Item.RowId == darkMatterItemId)
                return Math.Max(1, (int)grade.RowId);

        return 1;
    }

    public static bool RepairNPCNearby(out IGameObject? npc, float range = 7)
    {
        npc = null;
        if (Dalamud.Objects.LocalPlayer != null)
        {
            var nearest = float.MaxValue;
            foreach (var obj in Dalamud.Objects.Where(x => x.ObjectKind == ObjectKind.EventNpc))
            {
                var enpcsheet = Dalamud.GameData.Excel.GetSheet<ENpcBase>();
                if (enpcsheet != null && enpcsheet.TryGetRow(obj.BaseId, out var enpc))
                {
                    if (enpc.ENpcData.Any(x => x.RowId == 720915))
                    {
                        var npcDistance = Vector3.Distance(obj.Position, Dalamud.Objects.LocalPlayer.Position);
                        if (npcDistance > range || npcDistance >= nearest)
                            continue;

                        nearest = npcDistance;
                        npc     = obj;
                    }
                }
            }
        }
        return npc != null;
    }

    public static int GetNPCRepairPrice()
    {
        var output = 0;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                double actualCond = Math.Round(item->Condition / (float)300, 2);
                if (actualCond < 100)
                {
                    var itemSheet = Dalamud.GameData.Excel.GetSheet<Item>();
                    if (itemSheet != null && itemSheet.TryGetRow(item->ItemId, out var itemData))
                    {
                        var lvl = itemData.LevelEquip;
                        var condDif = (100 - actualCond) / 100;
                        var priceSheet = Dalamud.GameData.Excel.GetSheet<ItemRepairPrice>();
                        if (priceSheet != null && priceSheet.TryGetRow(lvl, out var priceData))
                        {
                            var price = Math.Round(priceData.Unknown0 * condDif, 0, MidpointRounding.ToPositiveInfinity);
                            output += (int)price;
                        }
                    }
                }
            }
        }
        return output;
    }

    public static bool RepairWindowOpen()
    {
        return GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var repairAddon) && repairAddon->AtkUnitBase.IsVisible;
    }

    public static void Repair()
    {
        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled && Throttler.Throttle("Repair", 500))
        {
            new AddonMaster.Repair((IntPtr)addon).RepairAll();
        }
    }

    public static void ConfirmYesNo()
    {
        if (GenericHelpers.TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            addon->AtkUnitBase.IsVisible &&
            addon->YesButton is not null)
        {
            new AddonMaster.SelectYesno((IntPtr)addon).Yes();
            GatherBuddy.Log.Debug("[RepairManager] Clicked Yes on SelectYesno");
        }
        else
        {
            GatherBuddy.Log.Debug("[RepairManager] SelectYesno not ready for confirmation");
        }
    }

    public static bool InteractWithRepairNPC()
    {
        if (RepairNPCNearby(out IGameObject? npc) && npc != null)
        {
            TargetSystem.Instance()->OpenObjectInteraction((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)npc.Address);
            if (GenericHelpers.TryGetAddonByName<AddonSelectIconString>("SelectIconString", out var addonSelectIconString))
            {
                var enpcsheet = Dalamud.GameData.Excel.GetSheet<ENpcBase>();
                if (enpcsheet != null && enpcsheet.TryGetRow(npc.BaseId, out var enpc))
                {
                    var repairDataList = enpc.ENpcData.ToList();
                    var repairData = repairDataList.FirstOrDefault(x => x.RowId == 720915);
                    var index = repairDataList.IndexOf(repairData);
                    Callback.Fire(&addonSelectIconString->AtkUnitBase, true, index);
                }
            }

            if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var addonRepair))
            {
                return true;
            }
        }
        return false;
    }

    public static bool NeedsRepair(int repairThreshold = 10)
    {
        return GetMinEquippedPercent() < repairThreshold;
    }
}
