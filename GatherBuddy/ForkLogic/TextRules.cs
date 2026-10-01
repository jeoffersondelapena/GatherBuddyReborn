#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GatherBuddy.ForkLogic;

public static class TextRules
{
    private static readonly Regex ChatTag = new(@"^\[GatherBuddy(Reborn)?\]\s*");

    public static string? RunLabel(IReadOnlyList<string> lists)
        => lists.Count switch
        {
            0 => null,
            1 => lists[0],
            2 => $"{lists[0]} + {lists[1]}",
            _ => $"{lists[0]} + {lists.Count - 1} more",
        };

    // the label goes after the plugin's own tag; a line that already names the list is left alone
    public static string WithRunLabel(string message, string? label)
    {
        if (string.IsNullOrEmpty(label) || message.Contains(label, StringComparison.Ordinal))
            return message;

        var tag = ChatTag.Match(message);
        return tag.Success ? $"{tag.Value.TrimEnd()} [{label}] {message[tag.Length..]}" : $"[{label}] {message}";
    }

    public static IEnumerable<string> ListLines(string header, IReadOnlyList<string> items, int max = int.MaxValue, string? footer = null)
    {
        yield return $"[GatherBuddy] {header} (fork)";
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
