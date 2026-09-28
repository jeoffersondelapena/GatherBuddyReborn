namespace GatherBuddy.Gui;

// Fork only: the in-game list reset closes the editor so a stale copy cannot be saved back over the restored list.
public partial class VulcanWindow
{
    public void CloseListEditor()
    {
        DisposeListEditor();
        _editingList = null;
    }
}
