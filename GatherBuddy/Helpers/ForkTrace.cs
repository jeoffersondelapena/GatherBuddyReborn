using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GatherBuddy.AutoHookIntegration.Models;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// Fork-only diagnostics; the tag keeps them greppable and out of upstream PRs.
public static class ForkTrace
{
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private static readonly object FileLock = new();
    private static string? _fileKey;
    private static string? _filePath;

    public static void Info(string message)
    {
        GatherBuddy.Log.Information($"[fork] {message}");
        Append(message);
    }

    // One file per character: two game windows fight over dalamud.log. Same key as Codex's state files.
    private static unsafe void Append(string message)
    {
        try
        {
            var cid = PlayerState.Instance()->ContentId;
            var key = cid == 0 ? "pre-login" : Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(cid)))[..16].ToLowerInvariant();
            lock (FileLock)
            {
                if (key != _fileKey)
                {
                    var dir = Dalamud.PluginInterface.ConfigDirectory.FullName;
                    Directory.CreateDirectory(dir);
                    _filePath = Path.Combine(dir, $"trace-{key}.log");
                    _fileKey  = key;
                }

                var file = new FileInfo(_filePath!);
                if (file.Exists && file.Length > MaxFileBytes)
                    file.MoveTo(Path.ChangeExtension(_filePath!, ".old.log"), true);
                File.AppendAllText(_filePath!, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}\n");
            }
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Debug($"[fork] trace file write failed: {ex.Message}");
        }
    }

    public static string Named(uint itemId)
        => $"{ItemName(itemId)} ({itemId})";

    public static string Named(int itemId)
        => itemId < 0 ? "none" : Named((uint)itemId);

    public static string ItemName(uint itemId)
    {
        var sheet = Dalamud.GameData.GetExcelSheet<Item>();
        if (sheet != null && sheet.TryGetRow(itemId, out var item))
            return item.Name.ExtractText();
        if (itemId >= 1_000_000 && sheet != null && sheet.TryGetRow(itemId - 1_000_000, out var hq))
            return hq.Name.ExtractText() + " HQ";
        return "unknown item";
    }

    public static string Describe(AHCustomPresetConfig preset)
    {
        var sb = new StringBuilder($"preset '{preset.PresetName}': bait ");
        sb.Append(preset.ListOfBaits.Count == 0 ? "none" : string.Join("; ", preset.ListOfBaits.Select(DescribeHook)));
        if (preset.ListOfMooch.Count > 0)
            sb.Append(" | mooch ").Append(string.Join("; ", preset.ListOfMooch.Select(DescribeHook)));
        var rules = preset.ListOfFish
            .Where(f => f.Enabled && (f.Mooch.Enabled || f.NeverMooch || f.SurfaceSlap.Enabled || f.IdenticalCast.Enabled))
            .ToList();
        if (rules.Count > 0)
            sb.Append(" | on catch ").Append(string.Join("; ", rules.Select(DescribeRule)));
        if (preset.ExtraCfg is { Enabled: true } extra)
            sb.Append(" | forced bait ").Append(extra.ForceBaitSwap ? Named(extra.ForcedBaitId) : "off");
        return sb.ToString();
    }

    private static string DescribeHook(AHHookConfig hook)
    {
        var tugs = Tugs(hook.NormalHook);
        var s    = $"{Named(hook.BaitFish.Id)} -> {(tugs.Length == 0 ? "no tug enabled" : tugs)}";
        if (hook.IntuitionHook.UseCustomStatusHook)
            s += $" (under intuition: {Tugs(hook.IntuitionHook)})";
        if (hook.NormalHook.CastLures is { Enabled: true } lure)
            s += $", lure {Named(lure.Id)}";
        if (hook.NormalHook.StopAfterCaught)
            s += $", stop after {hook.NormalHook.StopAfterCaughtLimit}";
        return s;
    }

    private static string Tugs(AHBaseHookset set)
    {
        var parts = new List<string>();
        AddTug(parts, "!", set.PatienceWeak);
        AddTug(parts, "!!", set.PatienceStrong);
        AddTug(parts, "!!!", set.PatienceLegendary);
        if (set.UseDoubleHook)
            parts.Add("double hook");
        if (set.UseTripleHook)
            parts.Add("triple hook");
        return string.Join(", ", parts);
    }

    private static void AddTug(List<string> parts, string tug, AHBaseBiteConfig bite)
    {
        if (!bite.HooksetEnabled)
            return;
        var s = $"{tug} {bite.HooksetType}";
        if (bite.HookTimerEnabled)
            s += $" {bite.MinHookTimer:0.#}-{bite.MaxHookTimer:0.#}s";
        parts.Add(s);
    }

    private static string DescribeRule(AHFishConfig fish)
    {
        var parts = new List<string>();
        if (fish.Mooch.Enabled)
            parts.Add("mooch");
        if (fish.NeverMooch)
            parts.Add("never mooch");
        if (fish.SurfaceSlap.Enabled)
            parts.Add("surface slap");
        if (fish.IdenticalCast.Enabled)
            parts.Add("identical cast");
        return $"{Named(fish.Fish.Id)}: {string.Join(", ", parts)}";
    }
}
