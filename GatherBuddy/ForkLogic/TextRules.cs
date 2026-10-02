#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GatherBuddy.ForkLogic;

public static class TextRules
{
    private static readonly Regex ChatTag    = new(@"^\[GatherBuddy(Reborn)?\]\s*");
    private static readonly Regex LeadingTag = new(@"^\[[^\]]*\]\s*");

    public static string? RunLabel(IReadOnlyList<string> lists)
        => lists.Count switch
        {
            0 => null,
            1 => lists[0],
            2 => $"{lists[0]} + {lists[1]}",
            _ => $"{lists[0]} + {lists.Count - 1} more",
        };

    public const string Crafting  = "Crafting";
    public const string Gathering = "Gathering";
    public const string BuyList   = "Buy list";

    // a crafting run that stops short of what it needs pauses; one button per part goes on without the rest
    public const string Retainers     = "Retainers";
    public const string SkipRetainers = "Skip Retainers (fork)";
    public const string SkipBuying    = "Skip Buying (fork)";
    public const string SkipGathering = "Skip Gathering (fork)";

    public static string StoppedShort(string part, string? why, bool gatheringRun = false)
    {
        var (what, resume, button, next) = Part(part);
        return $"[GatherBuddy] {what} for this {(gatheringRun ? "gathering" : "crafting")} run stopped: {Why(why)}. Fix the cause and press Resume to "
          + $"{resume}, or press {button} in the {(gatheringRun ? "Auto-Gather tab" : "Craft Status window")} to {next} (fork).";
    }

    public static string StoppedShortReason(string part, string? why)
    {
        var (what, resume, button, _) = Part(part);
        return $"{what} stopped: {Why(why)}. Fix the cause and press Resume to {resume}, or {button}.";
    }

    private static (string What, string Resume, string Button, string Next) Part(string part)
        => part switch
        {
            BuyList   => ("Buying", "buy the rest", SkipBuying, "go on to gathering and crafting without them"),
            Retainers => ("Taking from your retainers", "try again", SkipRetainers, "go on without them"),
            _         => ("Gathering", "gather the rest", SkipGathering, "craft with what is here"),
        };

    private static string Why(string? why)
        => string.IsNullOrWhiteSpace(why) ? "it could not go on" : why.Trim().TrimEnd('.');

    private static readonly string[] Kinds = [Crafting, Gathering, BuyList];

    // a crafting list and its generated buy list share a name, so the label says which kind of run it is
    public static string? KindLabel(string kind, string? name)
        => string.IsNullOrWhiteSpace(name) ? null : $"{kind}: {ShownLabel(name)}";

    public static string DividerStart(string label)
        => $"[GatherBuddy] ======== {label} STARTED (fork) ========";

    public static string DividerEnd(string label)
        => $"[GatherBuddy] ======== {label} ENDED (fork) ========";

    public static string KindOfVerb(string verb)
        => verb switch
        {
            "gathered" => Gathering,
            "bought"   => BuyList,
            _          => Crafting,
        };

    // the label goes after the plugin's own tag; a line that already names the list is left alone
    public static string WithRunLabel(string message, string? label)
    {
        if (string.IsNullOrEmpty(label) || message.Contains(label, StringComparison.Ordinal))
            return message;

        var shown = ShownLabel(label);
        var name  = Kinds.Select(k => $"{k}: ").FirstOrDefault(p => shown.StartsWith(p, StringComparison.Ordinal)) is { } kind ? shown[kind.Length..] : shown;
        if (message.Contains($"[{shown}]", StringComparison.Ordinal) || message.Contains(name, StringComparison.Ordinal))
            return message;

        var tag = ChatTag.Match(message);
        return tag.Success ? $"{tag.Value.TrimEnd()} [{shown}] {message[tag.Length..]}" : $"[{shown}] {message}";
    }

    // a generated buy list is named "[gbr-lists] ..."; in brackets of its own that read as "[[gbr-lists] ...]"
    public static string ShownLabel(string label)
        => LeadingTag.Replace(label, "") is { Length: > 0 } rest ? rest : label;

    public static string Tagged(string message)
        => ChatTag.IsMatch(message) || message.StartsWith('[') ? message : $"[GatherBuddy] {message}";

    public static string Capitalized(string name)
        => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    public static IEnumerable<string> ListLines(string header, IReadOnlyList<string> items, int max = int.MaxValue, string? footer = null)
    {
        yield return header.EndsWith(':') ? $"[GatherBuddy] {header[..^1]} (fork):" : $"[GatherBuddy] {header} (fork)";
        foreach (var item in items.Take(max))
            yield return $"    - {item}";
        if (items.Count > max)
            yield return $"    - and {items.Count - max} more";
        if (footer != null)
            yield return $"    {footer}";
    }

    public static string Brief(IReadOnlyList<string> names, int shown = 5)
        => string.Join(", ", names.Take(shown)) + (names.Count > shown ? $" and {names.Count - shown} more" : "");

    public static string FirstLine(string? reason, int limit = 90)
    {
        var line = (reason ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "unknown";
        return line.Length > limit ? line[..(limit - 3)] + "..." : line;
    }

    // NoSolution is the solver's answer for one recipe and one set of stats; anything else is the solver failing.
    public static bool IsNoSolution(string? reason)
        => reason?.Contains("NoSolution") == true;

    /// <summary>The attention file's lines after a source sets or clears its note; null when nothing changes.</summary>
    public static List<string>? WithNote(IEnumerable<string> lines, string source, string? note)
    {
        var kept = lines.Where(l => l.Trim().Length > 0).ToList();
        var mine = kept.FindIndex(l => l.StartsWith(source + ":"));
        if ((note != null) == (mine >= 0))
            return null;

        if (mine >= 0)
            kept.RemoveAt(mine);
        else
            kept.Add($"{source}: {note}");
        return kept;
    }
}
