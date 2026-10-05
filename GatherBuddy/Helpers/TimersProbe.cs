using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Automation;

namespace GatherBuddy.Helpers;

// fork: temporary. Traces what the game sends when a Timers row is chosen and what a turn-in changes, so the mission
// lists button can open the missions page itself and leave out a delivered mission.
internal static unsafe class TimersProbe
{
    private const string Timers = "ContentsInfo";
    private const int    Lines  = 300;

    private static Hook<AtkUnitBase.Delegates.FireCallback>?    _callbackHook;
    private static Hook<AgentInterface.Delegates.ReceiveEvent>? _agentHook;
    private static DateTime                                     _polled = DateTime.MinValue;
    private static string                                       _delivery = string.Empty;
    private static string                                       _page     = string.Empty;
    private static string                                       _lastWindowEvent = string.Empty;
    private static int                                          _windowEvents;
    private static int                                          _changes;

    public static void Start()
    {
        try
        {
            _callbackHook = Dalamud.Interop.HookFromAddress<AtkUnitBase.Delegates.FireCallback>(
                (nint)AtkUnitBase.MemberFunctionPointers.FireCallback, OnCallback);
            _callbackHook.Enable();
            var agent = AgentContentsTimer.Instance();
            if (agent != null)
            {
                _agentHook = Dalamud.Interop.HookFromAddress<AgentInterface.Delegates.ReceiveEvent>(
                    (nint)agent->VirtualTable->ReceiveEvent, OnAgentEvent);
                _agentHook.Enable();
            }

            Dalamud.AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, Timers, OnWindowEvent);
            ForkTrace.Info($"timers probe: started, agent hook {(_agentHook != null ? "on" : "off")}");
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[TimersProbe] not started: {e.Message}");
        }
    }

    public static void Stop()
    {
        Dalamud.AddonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, Timers, OnWindowEvent);
        _callbackHook?.Dispose();
        _agentHook?.Dispose();
        _callbackHook = null;
        _agentHook    = null;
    }

    private static bool OnCallback(AtkUnitBase* addon, uint valueCount, AtkValue* values, bool close)
    {
        try
        {
            if (addon != null && addon->NameString.StartsWith(Timers, StringComparison.Ordinal))
                ForkTrace.Info($"timers probe: {addon->NameString} fired a callback, close {close}: {Describe(values, valueCount)}");
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[TimersProbe] {e.Message}");
        }

        return _callbackHook!.Original(addon, valueCount, values, close);
    }

    private static AtkValue* OnAgentEvent(AgentInterface* agent, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind)
    {
        try
        {
            if (agent == (AgentInterface*)AgentContentsTimer.Instance())
                ForkTrace.Info($"timers probe: the Timers agent got event kind {eventKind}: {Describe(values, valueCount)}");
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[TimersProbe] {e.Message}");
        }

        return _agentHook!.Original(agent, returnValue, values, valueCount, eventKind);
    }

    // hovering sends a stream of these, so a repeat is dropped and the count is capped
    private static void OnWindowEvent(AddonEvent type, AddonArgs args)
    {
        if (args is not AddonReceiveEventArgs received || _windowEvents >= Lines)
            return;

        var data = received.AtkEventData != nint.Zero ? Hex((byte*)received.AtkEventData, 0, 32) : "none";
        var text = $"type {received.AtkEventType}, param {received.EventParam}, data {data}";
        if (text == _lastWindowEvent)
            return;

        _lastWindowEvent = text;
        _windowEvents++;
        ForkTrace.Info($"timers probe: {Timers} got a window event: {text}");
    }

    public static void Watch()
    {
        if (DateTime.UtcNow - _polled < TimeSpan.FromSeconds(1) || _changes >= Lines)
            return;

        _polled = DateTime.UtcNow;
        try
        {
            var agent = AgentGrandCompanySupply.Instance();
            if (agent != null && agent->IsAgentActive() && agent->SupplyProvisioningData != null
             && Changed(ref _delivery, Delivery(agent->SupplyProvisioningData)))
                ForkTrace.Info($"timers probe: delivery data, tab {agent->SelectedTab}: {_delivery}");

            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(GcMissions.Window, out var page) && page->IsVisible
             && GcMissions.WindowShowsMissions(page) && Changed(ref _page, GcMissions.Numbers(page)))
                ForkTrace.Info($"timers probe: missions page numbers: {_page}");
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[TimersProbe] {e.Message}");
        }
    }

    private static bool Changed(ref string last, string now)
    {
        if (last == now)
            return false;

        last = now;
        _changes++;
        return true;
    }

    private static string Delivery(SupplyProvisioningData* data)
    {
        var text = new StringBuilder($"head {Hex((byte*)data, 0, 64)}");
        foreach (ref var item in data->SupplyData)
            Add(text, ref item);
        foreach (ref var item in data->ProvisioningData)
            Add(text, ref item);
        return text.Append($" | tail {Hex((byte*)data, 1912, 24)}").ToString();
    }

    private static void Add(StringBuilder text, ref SupplyProvisioningItem item)
    {
        var bytes = (byte*)Unsafe.AsPointer(ref item);
        text.Append($" | {item.ItemId} x{item.NumRequested} {Hex(bytes, 12, 28)} {Hex(bytes, 148, 20)}");
    }

    private static string Hex(byte* bytes, int from, int count)
    {
        var text = new StringBuilder(count * 2);
        for (var i = 0; i < count; i++)
            text.Append(bytes[from + i].ToString("x2"));
        return text.ToString();
    }

    private static string Describe(AtkValue* values, uint count)
    {
        if (values == null)
            return $"{count} value(s), none readable";

        var parts = new List<string>();
        for (var i = 0; i < Math.Min(count, 16u); i++)
        {
            var value = values[i];
            parts.Add(((int)value.Type & 0x0F) switch
            {
                2       => $"bool {value.Byte != 0}",
                3       => $"int {value.Int}",
                5       => $"uint {value.UInt}",
                7       => $"float {value.Float}",
                8 or 10 => (byte*)value.String != null ? $"text '{SeString.Parse((byte*)value.String).TextValue}'" : "text none",
                _       => $"type {(int)value.Type} raw {*(ulong*)((byte*)&value + 8):x}",
            });
        }

        return $"{count} value(s): {string.Join(", ", parts)}";
    }
}
