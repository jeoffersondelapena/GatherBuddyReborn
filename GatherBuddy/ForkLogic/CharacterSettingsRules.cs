#nullable enable
using System.Text.Json;

namespace GatherBuddy.ForkLogic;

// what one character chose for itself; a value left out follows the shared setting, and 0 (roulette) is a choice
public static class CharacterSettingsRules
{
    public sealed class Own
    {
        public uint? MountId { get; set; }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static uint Mount(Own? own, uint shared)
        => own?.MountId ?? shared;

    public static string Serialize(Own own)
        => JsonSerializer.Serialize(own, Indented);

    public static Own Deserialize(string text)
        => JsonSerializer.Deserialize<Own>(text) ?? new Own();
}
