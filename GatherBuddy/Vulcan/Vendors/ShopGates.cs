using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Vulcan.Vendors;

// fork: a gil shop the game keeps shut until a quest is done is no shop to a run, as the Sahagin vendor's was
internal static class ShopGates
{
    private static Dictionary<uint, uint>?                _shopQuest;
    private static Dictionary<(uint Shop, uint Item), uint[]>? _itemQuests;
    private static readonly object Lock = new();

    private static void EnsureBuilt()
    {
        if (_shopQuest != null)
            return;

        lock (Lock)
        {
            if (_shopQuest != null)
                return;

            var shopQuest  = new Dictionary<uint, uint>();
            var itemQuests = new Dictionary<(uint, uint), uint[]>();
            try
            {
                var shops = Dalamud.GameData.GetExcelSheet<GilShop>();
                var items = Dalamud.GameData.GetSubrowExcelSheet<GilShopItem>();
                foreach (var shop in shops ?? Enumerable.Empty<GilShop>())
                    if (shop.Quest.RowId != 0)
                        shopQuest[shop.RowId] = shop.Quest.RowId;
                foreach (var row in items == null ? Enumerable.Empty<GilShopItem>() : items.SelectMany(s => s))
                {
                    var quests = row.QuestRequired.Where(q => q.RowId != 0).Select(q => q.RowId).ToArray();
                    if (quests.Length > 0 && row.Item.RowId != 0)
                        itemQuests[(row.RowId, row.Item.RowId)] = quests;
                }
            }
            catch (Exception e)
            {
                GatherBuddy.Log.Warning($"[ShopGates] the shop gates could not be read: {e.Message}");
            }

            _itemQuests = itemQuests;
            _shopQuest  = shopQuest;
        }
    }

    /// <summary>Whether this character cannot buy from the shop yet, naming the quest it waits on; itemId 0 checks the shop alone.</summary>
    public static bool Locked(uint shopId, uint itemId, out string quest)
    {
        EnsureBuilt();
        quest = string.Empty;
        var pending = new List<uint>();
        if (_shopQuest!.TryGetValue(shopId, out var shopGate))
            pending.Add(shopGate);
        if (itemId != 0 && _itemQuests!.TryGetValue((shopId, itemId), out var itemGates))
            pending.AddRange(itemGates);

        var waiting = pending.FirstOrDefault(id => !QuestManager.IsQuestComplete(id));
        if (waiting == 0)
            return false;

        quest = Dalamud.GameData.GetExcelSheet<Quest>()?.GetRowOrDefault(waiting)?.Name.ExtractText() is { Length: > 0 } name ? name : $"quest {waiting}";
        return true;
    }
}
