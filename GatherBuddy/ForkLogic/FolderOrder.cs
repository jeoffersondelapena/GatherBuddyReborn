#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// folders named after a class follow the character window (its ClassJob row order); other folders keep the alphabet, after them
public static class FolderOrder
{
    public static IEnumerable<T> Sort<T>(IEnumerable<T> folders, Func<T, string> name, IReadOnlyDictionary<string, uint> classRank)
        => folders.OrderBy(f => classRank.TryGetValue(name(f), out var rank) ? rank : uint.MaxValue)
            .ThenBy(name, StringComparer.OrdinalIgnoreCase);
}
