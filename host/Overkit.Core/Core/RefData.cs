using System.Text.Json;
using System.Text.RegularExpressions;

namespace Overkit.Host.Core;

/// <summary>
/// Données de référence du dataset (§2.4) : noms d'espèces et de passifs
/// localisés, CombiRank… Chargées depuis le dataset (P6) ; absentes, les
/// identifiants internes aérés servent de repli — jamais de crash.
/// </summary>
public sealed class RefData
{
    private readonly Dictionary<string, string> _speciesNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _passiveNames = new(StringComparer.OrdinalIgnoreCase);

    public int SpeciesCount => _speciesNames.Count;

    public string SpeciesName(string speciesId) =>
        _speciesNames.TryGetValue(speciesId, out var name) ? name : Prettify(speciesId);

    public string PassiveName(string passiveId) =>
        _passiveNames.TryGetValue(passiveId, out var name) ? name : Prettify(passiveId);

    public static RefData Load(Action<string> log)
    {
        var refData = new RefData();
        var dir = FindDatasetDirectory();
        if (dir is null)
        {
            log("Dataset absent : identifiants internes utilisés comme noms");
            return refData;
        }

        try
        {
            refData.LoadNames(Path.Combine(dir, "pals.json"), "pals", refData._speciesNames);
            refData.LoadNames(Path.Combine(dir, "passives.json"), "passives", refData._passiveNames);
            log($"Dataset chargé ({dir}) : {refData._speciesNames.Count} espèces, {refData._passiveNames.Count} passifs");
        }
        catch (Exception ex) when (ex is JsonException or IOException or KeyNotFoundException)
        {
            log($"Dataset illisible ({ex.Message}) : repli sur les identifiants internes");
        }
        return refData;
    }

    private void LoadNames(string path, string rootKey, Dictionary<string, string> target)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in doc.RootElement.GetProperty(rootKey).EnumerateObject())
        {
            if (entry.Value.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } value)
            {
                target[entry.Name] = value;
            }
        }
    }

    /// <summary>data/ à côté de l'exécutable (installation), sinon dataset-local/out en remontant (dev).</summary>
    private static string? FindDatasetDirectory()
    {
        var installed = Path.Combine(AppContext.BaseDirectory, "data");
        if (File.Exists(Path.Combine(installed, "pals.json")))
        {
            return installed;
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "dataset-local", "out");
            if (File.Exists(Path.Combine(candidate, "pals.json")))
            {
                return candidate;
            }
        }
        return null;
    }

    private static string Prettify(string id) =>
        Regex.Replace(id.Replace('_', ' ').Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ");
}
