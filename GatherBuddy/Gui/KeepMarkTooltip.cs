using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Automation;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

// node layout adapted from Price Insight (MIT, (c) 2021-2022 Kouzukii), see THIRD-PARTY-NOTICES.md; own node id, so both lines stack
internal sealed unsafe class KeepMarkTooltip : IDisposable
{
    private const string AddonName = "ItemDetail";
    private const string SetPositionSignature = "E8 ?? ?? ?? ?? 45 85 ED 4C 8B AC 24";
    private const uint   NodeId = 32713;
    private const int    Gap    = 4;

    private readonly delegate* unmanaged[Thiscall]<AtkUnitBase*, short, short, byte, void> _setPosition;
    private bool _failed;

    public KeepMarkTooltip()
    {
        try
        {
            _setPosition = (delegate* unmanaged[Thiscall]<AtkUnitBase*, short, short, byte, void>)Dalamud.SigScanner.ScanText(SetPositionSignature);
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[KeepMarkTooltip] tooltip move function not found, a tall tooltip may run off screen: {e.Message}");
        }

        Dalamud.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreUpdate);
        Dalamud.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnPostUpdate);
    }

    public void Dispose()
    {
        Dalamud.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreUpdate);
        Dalamud.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, AddonName, OnPostUpdate);
        if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(AddonName, out var addon))
            Restore(addon);
    }

    private void OnPreUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon != null)
            Restore(addon);
    }

    private void OnPostUpdate(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || KeepMarks.Count == 0 || !KeepMarks.TryGet(Dalamud.GameGui.HoveredItem, out var mark))
            return;

        try
        {
            Show(addon, KeepRules.TooltipLine(mark));
        }
        catch (Exception e)
        {
            if (!_failed)
                GatherBuddy.Log.Error($"[KeepMarkTooltip] keep line not added to the tooltip:\n{e}");
            _failed = true;
        }
    }

    private static AtkTextNode* Find(AtkUnitBase* addon)
    {
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node != null && node->NodeId == NodeId)
                return (AtkTextNode*)node;
        }

        return null;
    }

    private static void Restore(AtkUnitBase* addon)
    {
        var node = Find(addon);
        if (node == null || !node->AtkResNode.IsVisible())
            return;

        node->AtkResNode.ToggleVisibility(false);
        var below = addon->GetNodeById(2);
        if (below == null || addon->WindowNode == null)
            return;

        SetWindowHeight(addon, (ushort)(addon->WindowNode->AtkResNode.Height - node->AtkResNode.Height - Gap), false);
        below->SetYFloat(below->Y - node->AtkResNode.Height - Gap);
    }

    private void Show(AtkUnitBase* addon, string text)
    {
        var below = addon->GetNodeById(2);
        if (below == null || addon->WindowNode == null)
            return;

        var node = Find(addon);
        if (node == null && (node = Create(addon, below)) == null)
            return;

        node->AtkResNode.ToggleVisibility(true);
        node->SetText(new SeStringBuilder().AddText(text).Build().Encode());
        node->ResizeNodeForCurrentText();
        node->AtkResNode.SetYFloat(addon->WindowNode->AtkResNode.Height - 8);
        SetWindowHeight(addon, (ushort)(addon->WindowNode->AtkResNode.Height + node->AtkResNode.Height + Gap), true);
        below->SetYFloat(below->Y + node->AtkResNode.Height + Gap);

        var spare = ImGui.GetMainViewport().WorkSize.Y - addon->Y - addon->GetScaledHeight(true) - 36;
        if (spare >= 0)
            return;

        if (_setPosition != null)
            _setPosition(addon, addon->X, (short)(addon->Y + spare), 1);
        else
            addon->SetPosition(addon->X, (short)(addon->Y + spare));
    }

    private static void SetWindowHeight(AtkUnitBase* addon, ushort height, bool root)
    {
        addon->WindowNode->AtkResNode.SetHeight(height);
        var frame = addon->WindowNode->Component->UldManager.RootNode;
        if (frame != null)
        {
            frame->SetHeight(height);
            if (frame->PrevSiblingNode != null)
                frame->PrevSiblingNode->SetHeight(height);
        }

        if (root && addon->RootNode != null)
            addon->RootNode->SetHeight(height);
    }

    private static AtkTextNode* Create(AtkUnitBase* addon, AtkResNode* below)
    {
        var style = addon->GetTextNodeById(44);
        if (style == null)
            return null;

        var node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
        if (node == null)
            return null;

        node->AtkResNode.Type      = NodeType.Text;
        node->AtkResNode.NodeId    = NodeId;
        node->AtkResNode.NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
        node->AtkResNode.X         = 16;
        node->AtkResNode.Width     = 50;
        node->AtkResNode.Color     = style->AtkResNode.Color;
        node->TextColor            = new ByteColor { R = 0x8C, G = 0xE6, B = 0x7A, A = 0xFF };
        node->EdgeColor            = style->EdgeColor;
        node->LineSpacing          = 18;
        node->FontSize             = 12;
        node->TextFlags            = style->TextFlags | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize;

        var prev = below->PrevSiblingNode;
        node->AtkResNode.ParentNode      = below->ParentNode;
        node->AtkResNode.PrevSiblingNode = prev;
        node->AtkResNode.NextSiblingNode = below;
        below->PrevSiblingNode           = (AtkResNode*)node;
        if (prev != null)
            prev->NextSiblingNode = (AtkResNode*)node;
        addon->UldManager.UpdateDrawNodeList();
        return node;
    }
}
