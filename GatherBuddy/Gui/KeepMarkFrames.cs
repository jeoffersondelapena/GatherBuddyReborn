using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Helpers;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Gui;

// drawn over the bag grid instead of tinting its nodes, so another plugin's highlight on the same slots is never overwritten
internal static unsafe class KeepMarkFrames
{
    private const int SlotsPerPage = 35;

    private static readonly string[] LargeGrids     = ["InventoryGrid0", "InventoryGrid1"];
    private static readonly string[] ExpansionGrids = ["InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", "InventoryGrid3E"];
    private static readonly string[] Overlays       = ["ItemDetail", "ContextMenu"];

    // the game's UIColor rows 32 and 67, so a tooltip line and its frame share one color
    public const ushort NeededColor = 32;
    public const ushort MadeColor   = 67;

    private static readonly Vector4 Orange = new(240 / 255f, 142 / 255f, 55 / 255f, 1f);
    private static readonly Vector4 Green  = new(160 / 255f, 248 / 255f, 116 / 255f, 1f);

    private static readonly Dictionary<uint, uint> Icons  = new();
    private static readonly HashSet<string>        Traced = new();
    private static readonly List<(Vector2 Min, Vector2 Max)> Covers = new();

    public static void Draw()
    {
        if (KeepMarks.Count == 0)
            return;

        try
        {
            var module = UIModule.Instance();
            var order  = module == null ? null : module->GetItemOrderModule();
            if (order == null || order->InventorySorter == null)
                return;

            Covers.Clear();
            foreach (var name in Overlays)
                if (Visible<AtkUnitBase>(name, out var overlay))
                    Covers.Add(Rect(overlay));

            if (Visible<AddonInventory>("Inventory", out var normal))
                DrawGrid("InventoryGrid", normal->TabIndex, order->InventorySorter);
            else if (Visible<AddonInventoryLarge>("InventoryLarge", out var large))
                for (var i = 0; i < LargeGrids.Length; i++)
                    DrawGrid(LargeGrids[i], large->TabIndex * 2 + i, order->InventorySorter);
            else if (Visible<AddonInventoryExpansion>("InventoryExpansion", out _))
                for (var i = 0; i < ExpansionGrids.Length; i++)
                    DrawGrid(ExpansionGrids[i], i, order->InventorySorter);
        }
        catch (Exception e)
        {
            TraceOnce("error", $"keep frames: not drawn: {e.Message}");
        }
    }

    private static void DrawGrid(string name, int page, ItemOrderModuleSorter* sorter)
    {
        if (page is < 0 or > 3 || !Visible<AddonInventoryGrid>(name, out var grid))
            return;

        var inventory = InventoryManager.Instance();
        if (inventory == null)
            return;

        var perPage = sorter->ItemsPerPage > 0 ? sorter->ItemsPerPage : SlotsPerPage;
        for (var slot = 0; slot < SlotsPerPage; slot++)
        {
            var index = page * perPage + slot;
            if (index >= sorter->Items.LongCount)
                return;

            var entry = sorter->Items[index].Value;
            if (entry == null || entry->Page > 3)
                continue;

            var item = inventory->GetInventorySlot((InventoryType)((uint)InventoryType.Inventory1 + entry->Page), entry->Slot);
            if (item == null || item->ItemId == 0 || !KeepMarks.TryGet(item->GetBaseItemId(), out var needed, out _))
                continue;

            var dragDrop = grid->Slots[slot].Value;
            if (dragDrop == null || dragDrop->AtkComponentBase.OwnerNode == null)
                continue;

            // the bag order is read from the game's sort table; a slot showing another item's icon means the reading is off
            var shown = dragDrop->AtkComponentIcon == null ? 0 : dragDrop->AtkComponentIcon->IconId % 1_000_000;
            var icon  = IconOf(item->GetBaseItemId());
            TraceOnce($"{name}:{shown == icon}", $"keep frames: {name} page {page} slot {slot} holds bag {entry->Page} slot {entry->Slot}, "
              + $"{ForkTrace.Named(item->GetBaseItemId())}; icon shown {shown}, item icon {icon}: {(shown == icon ? "match" : "MISMATCH, not framed")}");
            if (shown == icon)
                Frame(&dragDrop->AtkComponentBase.OwnerNode->AtkResNode, needed != null ? Orange : Green);
        }
    }

    private static void Frame(AtkResNode* node, Vector4 color)
    {
        if (!node->IsVisible())
            return;

        var scale = Vector2.One;
        for (var n = node; n != null; n = n->ParentNode)
            scale *= new Vector2(n->ScaleX, n->ScaleY);

        var min = ImGui.GetMainViewport().Pos + new Vector2(node->ScreenX, node->ScreenY);
        var max = min + new Vector2(node->Width, node->Height) * scale;
        foreach (var (a, b) in Covers)
            if (min.X < b.X && max.X > a.X && min.Y < b.Y && max.Y > a.Y)
                return;

        var draw = ImGui.GetBackgroundDrawList();
        draw.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(color with { W = 0.16f }), 4f);
        draw.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(color), 4f, ImDrawFlags.None, 2f);
    }

    private static (Vector2, Vector2) Rect(AtkUnitBase* addon)
    {
        var min = ImGui.GetMainViewport().Pos + new Vector2(addon->X, addon->Y);
        return (min, min + new Vector2(addon->GetScaledWidth(true), addon->GetScaledHeight(true)));
    }

    private static bool Visible<T>(string name, out T* addon) where T : unmanaged
    {
        addon = (T*)(nint)Dalamud.GameGui.GetAddonByName(name);
        return addon != null && ((AtkUnitBase*)addon)->IsVisible;
    }

    private static uint IconOf(uint itemId)
    {
        if (!Icons.TryGetValue(itemId, out var icon))
            Icons[itemId] = icon = Dalamud.GameData.GetExcelSheet<Item>().GetRowOrDefault(itemId)?.Icon ?? 0u;
        return icon;
    }

    private static void TraceOnce(string key, string line)
    {
        if (Traced.Add(key))
            ForkTrace.Info(line);
    }
}
