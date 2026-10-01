using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// Fork only. The list generator names class folders in English; the client may not, so both names count.
public static class ClassFolderOrder
{
    private static Dictionary<string, uint>? _ranks;

    public static IReadOnlyDictionary<string, uint> Ranks
        => _ranks ??= Build();

    private static Dictionary<string, uint> Build()
    {
        var ranks = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in Dalamud.GameData.GetExcelSheet<ClassJob>())
        {
            if (job.ClassJobCategory.RowId is not (32 or 33))
                continue;

            ranks.TryAdd(job.Name.ExtractText(), job.RowId);
            ranks.TryAdd(job.NameEnglish.ExtractText(), job.RowId);
        }

        return ranks;
    }
}
