using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.AutoGather.Lists;

// Fork only: swap every generated list for the copy in the generator's snapshot.
public partial class AutoGatherListsManager
{
    public int ReplaceGenerated(string tag, IEnumerable<AutoGatherList.Config> configs)
    {
        foreach (var list in Lists.Where(l => (l.Description ?? string.Empty).StartsWith(tag)).ToList())
            if (_fileSystem.TryGetValue(list, out var leaf))
                _fileSystem.Delete(leaf);

        var added = 0;
        foreach (var cfg in configs)
        {
            AutoGatherList.FromConfig(cfg, out var list);
            var folder = _fileSystem.Root;
            foreach (var name in (list.FolderPath ?? string.Empty).Split('/', System.StringSplitOptions.RemoveEmptyEntries))
                (folder, _) = _fileSystem.FindOrCreateFolder(folder, name);
            try
            {
                _fileSystem.CreateLeaf(folder, list.Name, list);
            }
            catch
            {
                _fileSystem.CreateDuplicateLeaf(folder, list.Name, list);
            }
            added++;
        }

        Save();
        SetActiveItems();
        return added;
    }
}
