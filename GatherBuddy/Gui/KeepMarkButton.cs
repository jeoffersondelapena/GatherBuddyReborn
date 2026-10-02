using System.Numerics;
using Dalamud.Bindings.ImGui;
using ElliLib;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

internal static class KeepMarkButton
{
    private const string Orange = "Orange: items a paused crafting run still needs from your bags.\n"
      + "They also clear by themselves when the run goes on (Resume, a Skip button, or Stop).";

    private const string Green = "Green: what a run's list counts toward its targets, including what you already had:\n"
      + "a crafting list's own recipes (not their materials or in-between crafts), a gathering list's items, a buy list's items.\n"
      + "A run that is still going counts them again whenever they change; once it has ended, a clear stays.";

    public static void DrawBoth()
    {
        var room = ImGui.GetContentRegionAvail().X;
        DrawOrange();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var green   = ImGui.CalcTextSize("Clear Green Marks (fork)").X + ImGui.GetStyle().FramePadding.X * 2;
        if (room - ImGui.GetItemRectSize().X - spacing >= green)
            ImGui.SameLine();
        DrawGreen();
    }

    public static void DrawOrange()
    {
        var count = KeepMarks.NeededCount;
        if (ImGuiUtil.DrawDisabledButton("Clear Orange Marks (fork)", Vector2.Zero,
                $"{Orange}\n\n{(count == 0 ? "Nothing is marked orange." : $"{count} item(s) marked orange.")}", count == 0))
            KeepMarks.ClearNeeded();
    }

    public static void DrawGreen()
    {
        var count = KeepMarks.MadeCount;
        if (ImGuiUtil.DrawDisabledButton("Clear Green Marks (fork)", Vector2.Zero,
                $"{Green}\n\n{(count == 0 ? "Nothing is marked green." : $"{count} item(s) marked green.")}", count == 0))
            KeepMarks.ClearMade();
    }
}
