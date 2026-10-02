using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// fork: vendors sell normal quality, so only normal-quality stacks of the same item can take more
internal static unsafe class BagRoom
{
    private static readonly InventoryType[] Bags = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];

    public static int FreeSlots()
    {
        var inventory = InventoryManager.Instance();
        return inventory == null ? 0 : (int)inventory->GetEmptySlotsInBag();
    }

    public static int StackSize(uint itemId)
        => (int)(Dalamud.GameData.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.StackSize ?? 1);

    public static int Room(uint itemId, int stackSize)
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null)
            return 0;

        var room = 0;
        foreach (var bag in Bags)
        {
            var container = inventory->GetInventoryContainer(bag);
            if (container == null)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId == itemId && (slot->Flags & InventoryItem.ItemFlags.HighQuality) == 0)
                    room += System.Math.Max(0, stackSize - (int)slot->Quantity);
            }
        }

        return room;
    }
}
