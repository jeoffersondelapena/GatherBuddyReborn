using System;
using Dalamud.Bindings.ImGui;

namespace GatherBuddy.Gui;

// fork: a caption and one radio per line; a greyed option stays visible, and the dot marks what the run actually does
internal static class ForkChoice
{
    public readonly record struct Option(string Label, string Tip, bool Enabled = true);

    public static void Draw(string caption, string id, int effective, ReadOnlySpan<Option> options, Action<int> choose)
    {
        ImGui.TextUnformatted(caption);
        ImGui.Indent();
        for (var i = 0; i < options.Length; i++)
        {
            var option = options[i];
            ImGui.BeginDisabled(!option.Enabled);
            var clicked = ImGui.RadioButton($"{option.Label}##{id}{i}", effective == i);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(option.Tip);
            if (clicked && option.Enabled && effective != i)
                choose(i);
        }

        ImGui.Unindent();
    }
}
