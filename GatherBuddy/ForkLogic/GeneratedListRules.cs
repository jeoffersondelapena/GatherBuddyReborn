#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// loading the generator's lists in game leaves no empty generated folders behind
public static class GeneratedListRules
{
    public static List<string> Folders(IEnumerable<string> current, IEnumerable<string> added, IEnumerable<string> dropped, IEnumerable<string> listFolders)
    {
        var inUse = listFolders.Where(f => f.Length > 0).SelectMany(Ancestors).ToHashSet(StringComparer.Ordinal);
        var gone  = dropped.Where(f => !inUse.Contains(f)).ToHashSet(StringComparer.Ordinal);
        return current.Concat(added).Where(f => !gone.Contains(f)).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> Ancestors(string path)
    {
        var parts = path.Split('/');
        for (var k = 1; k <= parts.Length; k++)
            yield return string.Join('/', parts[..k]);
    }
}
