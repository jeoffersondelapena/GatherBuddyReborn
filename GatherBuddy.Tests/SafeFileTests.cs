using GatherBuddy.ForkLogic;
using Xunit;

public sealed class SafeFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gbr-safefile-").FullName;

    public void Dispose()
        => Directory.Delete(_dir, true);

    private string PathOf(string name)
        => Path.Combine(_dir, name);

    [Fact]
    public void A_write_replaces_the_file_and_leaves_nothing_beside_it()
    {
        var path = PathOf("gather-state-first.json");
        SafeFile.Write(path, "{ \"first\": true }");
        SafeFile.Write(path, "{ \"second\": true }");

        Assert.Equal("{ \"second\": true }", SafeFile.Read(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void A_missing_file_fails_at_once_instead_of_waiting()
    {
        var started = DateTime.UtcNow;
        Assert.Throws<FileNotFoundException>(() => SafeFile.Read(PathOf("absent.json"), attempts: 5, waitMs: 2000));
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void A_failed_write_keeps_the_old_file_and_cleans_up()
    {
        var path = PathOf("auto_gather_lists.json");
        SafeFile.Write(path, "old");
        Assert.ThrowsAny<IOException>(() => SafeFile.Write(Path.Combine(_dir, "no-such-folder", "x.json"), "new"));

        Assert.Equal("old", SafeFile.Read(path));
        Assert.Equal(new[] { path }, Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task A_reader_never_sees_half_a_file_while_another_window_writes()
    {
        var path  = PathOf("auto_gather_lists.json");
        var texts = new[] { new string('a', 400_000), new string('b', 400_000) };
        SafeFile.Write(path, texts[0]);

        using var stop = new CancellationTokenSource();
        var writer = Task.Run(() =>
        {
            for (var i = 0; !stop.IsCancellationRequested; i++)
                SafeFile.Write(path, texts[i % 2]);
        });

        try
        {
            for (var i = 0; i < 300; i++)
                Assert.Contains(SafeFile.Read(path), texts);
        }
        finally
        {
            stop.Cancel();
            await writer;
        }
    }
}
