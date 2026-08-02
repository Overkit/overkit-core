using System.Text.Json;

namespace Overkit.Host.Core;

/// <summary>
/// Réglages du host. Valeurs par défaut saines ; surcharge optionnelle via
/// overkit.settings.json à côté de l'exécutable.
/// </summary>
public sealed record HostSettings
{
    public string ProbeUri { get; init; } = "ws://127.0.0.1:47800";

    /// <summary>Virtual-key du hotkey panneau (défaut : F6 = 0x75). Remappable (§2.2).</summary>
    public uint PanelHotkeyVk { get; init; } = 0x75;

    public static HostSettings Load(Action<string> log)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "overkit.settings.json");
        if (!File.Exists(path))
        {
            return new HostSettings();
        }
        try
        {
            var settings = JsonSerializer.Deserialize<HostSettings>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            log($"Réglages chargés : {path}");
            return settings ?? new HostSettings();
        }
        catch (JsonException ex)
        {
            log($"overkit.settings.json invalide ({ex.Message}) : réglages par défaut");
            return new HostSettings();
        }
    }
}
