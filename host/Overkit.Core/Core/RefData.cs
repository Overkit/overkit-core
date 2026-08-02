using System.Text.Json;
using System.Text.RegularExpressions;

namespace Overkit.Host.Core;

public sealed record SpeciesInfo(string Id, string Name, int CombiRank, int ZukanIndex, string ZukanSuffix);

public sealed record RecipeInfo(string Key, string ProductId, int ProductCount, double WorkAmount,
                                IReadOnlyList<(string ItemId, int Count)> Materials);

public sealed record SpecialCombo(string ParentA, int GenderA, string ParentB, int GenderB, string Child);

public sealed record DropSource(string SpeciesId, double Rate, int Min, int Max);

/// <summary>
/// Données de référence du dataset (§2.4) : noms localisés, espèces (CombiRank,
/// Zukan), recettes, sources de butin, combos d'accouplement. Chargées depuis
/// le dataset (P6) ; absentes, repli sur identifiants aérés — jamais de crash.
/// </summary>
public sealed class RefData
{
    private readonly Dictionary<string, string> _speciesNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _passiveNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpeciesInfo> _species = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<DropSource>> _dropSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RecipeInfo> _recipes = [];
    private readonly List<SpecialCombo> _specialCombos = [];

    public int SpeciesCount => _speciesNames.Count;
    public IReadOnlyList<RecipeInfo> Recipes => _recipes;
    public IReadOnlyList<SpecialCombo> SpecialCombos => _specialCombos;
    public IReadOnlyCollection<SpeciesInfo> AllSpecies => _species.Values;

    public string SpeciesName(string speciesId) =>
        _speciesNames.TryGetValue(speciesId, out var name) ? name : Prettify(speciesId);

    public string PassiveName(string passiveId) =>
        _passiveNames.TryGetValue(passiveId, out var name) ? name : Prettify(passiveId);

    public string ItemName(string itemId) =>
        _itemNames.TryGetValue(itemId, out var name) ? name : Prettify(itemId);

    public bool TryGetSpecies(string speciesId, out SpeciesInfo info) =>
        _species.TryGetValue(speciesId, out info!);

    public IReadOnlyList<DropSource> DropSourcesFor(string itemId) =>
        _dropSources.TryGetValue(itemId, out var list) ? list : [];

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
            refData.LoadPals(Path.Combine(dir, "pals.json"));
            refData.LoadNames(Path.Combine(dir, "passives.json"), "passives", refData._passiveNames);
            refData.LoadNames(Path.Combine(dir, "items.json"), "items", refData._itemNames);
            refData.LoadRecipes(Path.Combine(dir, "recipes.json"));
            refData.LoadDrops(Path.Combine(dir, "drops.json"));
            refData.LoadBreeding(Path.Combine(dir, "breeding.json"));
            log($"Dataset chargé ({dir}) : {refData._species.Count} espèces, {refData._itemNames.Count} objets, " +
                $"{refData._recipes.Count} recettes, {refData._specialCombos.Count} combos");
        }
        catch (Exception ex) when (ex is JsonException or IOException or KeyNotFoundException)
        {
            log($"Dataset illisible ({ex.Message}) : repli sur les identifiants internes");
        }
        return refData;
    }

    private void LoadPals(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in doc.RootElement.GetProperty("pals").EnumerateObject())
        {
            var value = entry.Value;
            var name = value.TryGetProperty("name", out var n) ? n.GetString() ?? entry.Name : entry.Name;
            _speciesNames[entry.Name] = name;
            _species[entry.Name] = new SpeciesInfo(
                entry.Name,
                name,
                value.TryGetProperty("combi_rank", out var cr) ? cr.GetInt32() : 0,
                value.TryGetProperty("zukan_index", out var zi) ? zi.GetInt32() : -1,
                value.TryGetProperty("zukan_suffix", out var zs) ? zs.GetString() ?? "" : "");
        }
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

    private void LoadRecipes(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var entry in doc.RootElement.GetProperty("recipes").EnumerateObject())
        {
            var value = entry.Value;
            var materials = new List<(string, int)>();
            if (value.TryGetProperty("materials", out var mats))
            {
                foreach (var material in mats.EnumerateArray())
                {
                    materials.Add((material.GetProperty("item_id").GetString() ?? "",
                                   material.GetProperty("count").GetInt32()));
                }
            }
            _recipes.Add(new RecipeInfo(
                entry.Name,
                value.GetProperty("product_id").GetString() ?? entry.Name,
                value.GetProperty("product_count").GetInt32(),
                value.TryGetProperty("work_amount", out var w) ? w.GetDouble() : 0,
                materials));
        }
    }

    private void LoadDrops(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var species in doc.RootElement.GetProperty("drops").EnumerateObject())
        {
            foreach (var drop in species.Value.EnumerateArray())
            {
                var itemId = drop.GetProperty("item_id").GetString() ?? "";
                if (!_dropSources.TryGetValue(itemId, out var list))
                {
                    list = [];
                    _dropSources[itemId] = list;
                }
                list.Add(new DropSource(
                    species.Name,
                    drop.GetProperty("rate").GetDouble(),
                    drop.GetProperty("min").GetInt32(),
                    drop.GetProperty("max").GetInt32()));
            }
        }
        foreach (var list in _dropSources.Values)
        {
            list.Sort((a, b) => b.Rate.CompareTo(a.Rate));
        }
    }

    private void LoadBreeding(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var combo in doc.RootElement.GetProperty("special_combos").EnumerateArray())
        {
            _specialCombos.Add(new SpecialCombo(
                combo.GetProperty("parent_a").GetString() ?? "",
                combo.GetProperty("parent_gender_a").GetInt32(),
                combo.GetProperty("parent_b").GetString() ?? "",
                combo.GetProperty("parent_gender_b").GetInt32(),
                combo.GetProperty("child").GetString() ?? ""));
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
