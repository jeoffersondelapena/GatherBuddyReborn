using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Automation;

namespace GatherBuddy.Helpers;

// fork: temporary. Traces what a turn-in changes, so the mission lists can leave out a delivered mission.
internal static unsafe class TimersProbe
{
    private const int    Lines          = 300;
    private const int    Longest        = 6000;
    private const string DeliveryWindow = "GrandCompanySupplyList";

    private static DateTime _polled   = DateTime.MinValue;
    private static string   _delivery = string.Empty;
    private static string   _page     = string.Empty;
    private static int      _changes;

    public static void Watch()
    {
        if (DateTime.UtcNow - _polled < TimeSpan.FromSeconds(1) || _changes >= Lines)
            return;

        _polled = DateTime.UtcNow;
        try
        {
            if (GenericHelpers.TryGetAddonByName<AtkUnitBase>(DeliveryWindow, out var window) && window->IsVisible
             && Changed(ref _delivery, Describe(window)))
                ForkTrace.Info($"timers probe: delivery window: {_delivery}");

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

    private static string Describe(AtkUnitBase* window)
    {
        var agent = AgentGrandCompanySupply.Instance();
        var state = agent == null ? "no agent"
            : $"agent active {agent->IsAgentActive()}, tab {agent->SelectedTab}, data "
            + (agent->SupplyProvisioningData == null ? "none" : Delivery(agent->SupplyProvisioningData));
        var text = $"{state} | texts: {string.Join(" | ", GcMissions.Texts(window).Take(60))} | numbers: {GcMissions.Numbers(window)}";
        return text.Length > Longest ? text[..Longest] : text;
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
}
