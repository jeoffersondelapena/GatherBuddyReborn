#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// loading the generator's lists in game keeps each character's choices and leaves no empty generated folders behind
public static class GeneratedListRules
{
    public static List<string> Folders(IEnumerable<string> current, IEnumerable<string> added, IEnumerable<string> dropped, IEnumerable<string> listFolders)
    {
        var inUse = listFolders.Where(f => f.Length > 0).SelectMany(Ancestors).ToHashSet(StringComparer.Ordinal);
        var gone  = dropped.Where(f => !inUse.Contains(f)).ToHashSet(StringComparer.Ordinal);
        return current.Concat(added).Where(f => !gone.Contains(f)).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    public static T? Carried<T>(string key, IReadOnlyDictionary<string, T> before, IReadOnlyDictionary<string, string[]> former) where T : class
    {
        if (before.TryGetValue(key, out var same))
            return same;
        if (!former.TryGetValue(key, out var olds))
            return null;

        foreach (var old in olds)
            if (before.TryGetValue(old, out var found))
                return found;
        return null;
    }

    private static IEnumerable<string> Ancestors(string path)
    {
        var parts = path.Split('/');
        for (var k = 1; k <= parts.Length; k++)
            yield return string.Join('/', parts[..k]);
    }
}
