using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ElliLib.Log;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace GatherBuddy.Helpers;

// Fork only. Every plugin log line also lands in a per-character file: two game windows fight over dalamud.log.
public sealed class ForkLog : IDisposable
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const int  FlushMs      = 500;

    public Logger Inner { get; }

    private readonly ConcurrentQueue<string> _pending   = new();
    private readonly object                  _flushLock = new();
    private readonly Timer                   _timer;
    private          string?                 _key;
    private          string?                 _path;

    public ForkLog(Logger inner)
    {
        Inner  = inner;
        _timer = new Timer(_ => Flush(), null, FlushMs, FlushMs);
    }

    public void Fatal(string text)
    {
        Inner.Fatal(text);
        Enqueue("FTL", text);
    }

    public void Error(string text)
    {
        Inner.Error(text);
        Enqueue("ERR", text);
    }

    public void Warning(string text)
    {
        Inner.Warning(text);
        Enqueue("WRN", text);
    }

    public void Information(string text)
    {
        Inner.Information(text);
        Enqueue("INF", text);
    }

    public void Debug(string text)
    {
        Inner.Debug(text);
        Enqueue("DBG", text);
    }

    // Verbose is per-frame noise upstream; dalamud.log only.
    public void Verbose(string text)
        => Inner.Verbose(text);

    public void Verbose(string format, params object?[] args)
        => Inner.Verbose(format, args);

    public void Excessive(string text)
        => Inner.Excessive(text);

    public void Excessive(string format, params object?[] args)
        => Inner.Excessive(format, args);

    public void Flush()
    {
        if (_pending.IsEmpty)
            return;

        lock (_flushLock)
        {
            try
            {
                var path = CurrentPath();
                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaxFileBytes)
                    file.MoveTo(Path.ChangeExtension(path, ".old.log"), true);

                var sb = new StringBuilder();
                while (_pending.TryDequeue(out var line))
                    sb.Append(line).Append('\n');
                File.AppendAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Inner.Debug($"[fork] trace file write failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        Flush();
    }

    private void Enqueue(string level, string text)
        => _pending.Enqueue($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {text}");

    public const string NoCharacter = "pre-login";

    private static Tuple<ulong, string>? _keyFor;

    // Same key as Codex's state files, so one character's files line up across plugins.
    public static unsafe string CharacterKey()
    {
        if (Dalamud.ClientState is not { IsLoggedIn: true })
            return NoCharacter;

        var cid = PlayerState.Instance()->ContentId;
        if (cid == 0)
            return NoCharacter;

        var cached = _keyFor;
        if (cached == null || cached.Item1 != cid)
            _keyFor = cached = Tuple.Create(cid, Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(cid)))[..16].ToLowerInvariant());
        return cached.Item2;
    }

    private string CurrentPath()
    {
        var key = CharacterKey();
        if (key != _key)
        {
            var dir = Dalamud.PluginInterface.ConfigDirectory.FullName;
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, $"trace-{key}.log");
            _key  = key;
        }

        return _path!;
    }
}
