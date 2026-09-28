using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GatherBuddy.Plugin;
using Newtonsoft.Json.Linq;

namespace GatherBuddy.Helpers;

// Fork only. The settings checker leaves policy-snapshot.json beside the config; one click applies those values again.
public static class PolicySettings
{
    private static string SnapshotPath => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, "policy-snapshot.json");

    public static bool SnapshotExists => File.Exists(SnapshotPath);

    public static void Apply()
    {
        try
        {
            var snap    = JObject.Parse(File.ReadAllText(SnapshotPath));
            var options = snap["options"] as JObject ?? new JObject();
            var changed = new List<string>();
            var missing = new List<string>();
            foreach (var (key, value) in options)
            {
                if (value == null) continue;
                if (!TrySet(key, value, out var was)) { missing.Add(key); continue; }
                if (was) changed.Add(key);
            }

            GatherBuddy.Config.Save();
            var text = changed.Count == 0
                ? $"Settings already match the policy ({options.Count} checked)."
                : $"Settings reset to the policy: {changed.Count} changed ({string.Join(", ", changed)}).";
            if (missing.Count > 0)
                text += $" Not found in this version: {string.Join(", ", missing)}.";
            Communicator.Print(text);
            GatherBuddy.Log.Information($"[PolicySettings] {text}");
        }
        catch (Exception ex)
        {
            Communicator.PrintError($"Settings could not be reset: {ex.Message}");
            GatherBuddy.Log.Error($"[PolicySettings] Reset failed: {ex}");
        }
    }

    // "AutoGatherConfig.DoRepair" walks the config's properties; the last one is written when the value differs.
    private static bool TrySet(string path, JToken value, out bool changed)
    {
        changed = false;
        object owner = GatherBuddy.Config;
        var parts = path.Split('.');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var next = owner.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
            if (next == null) return false;
            owner = next;
        }

        var prop = owner.GetType().GetProperty(parts[^1], BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || !prop.CanWrite) return false;
        var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var wanted = target.IsEnum ? Enum.Parse(target, value.ToString()) : Convert.ChangeType(value.ToObject<object>(), target);
        if (Equals(prop.GetValue(owner), wanted)) return true;
        prop.SetValue(owner, wanted);
        changed = true;
        return true;
    }
}
