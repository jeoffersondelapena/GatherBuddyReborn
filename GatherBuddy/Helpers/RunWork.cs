namespace GatherBuddy.Helpers;

// fork: whether a run gathered or crafted anything, so a run that did neither skips the after-run repair
public static class RunWork
{
    public static int Gathered { get; private set; }
    public static int Crafted  { get; private set; }

    public static bool Any
        => Gathered > 0 || Crafted > 0;

    public static void Reset()
    {
        Gathered = 0;
        Crafted  = 0;
    }

    public static void NoteGathered()
        => Gathered++;

    public static void NoteCrafted()
        => Crafted++;
}
