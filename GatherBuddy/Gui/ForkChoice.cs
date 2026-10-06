using System;
using Dalamud.Bindings.ImGui;

namespace GatherBuddy.Gui;

// fork: a caption and one radio per line; a greyed option stays visible, and the dot marks what the run actually does
internal static class ForkChoice
{
    public readonly record struct Option(string Label, string Tip, bool Enabled = true);

    public static void Draw(string caption, string id, int effective, ReadOnlySpan<Option> options, Action<int> choose, Action? head = null)
    {
        ImGui.TextUnformatted(caption);
        ImGui.Indent();
        head?.Invoke();
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

    public static void Note(string text)
    {
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled(text);
        ImGui.PopTextWrapPos();
    }

    public const string MasterOff = "Buy From Vendors (fork) is off (fork page in the settings): nothing is bought, so the buying choices below are greyed, "
      + "and a material only a vendor sells stops a run before it sets out.";

    public const string MasterEverything = "Buy From Vendors (fork) buys whatever is sold on every list (fork page in the settings): the narrower choices "
      + "below are greyed until it is back on 'as each list says'.";

    public const string ByMasterOff = " Greyed: Buy From Vendors (fork) is off.";

    public const string ByMasterEverything = " Greyed: Buy From Vendors (fork) buys whatever is sold on every list.";
}
