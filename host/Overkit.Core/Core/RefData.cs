using Overkit.Sdk;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Overkit.Host.Core;

/// <summary>
/// Données de référence du dataset (§2.4) : noms localisés, espèces (CombiRank,
/// Zukan), recettes, sources de butin, combos d'accouplement. Chargées depuis
/// le dataset (P6) ; absentes, repli sur identifiants aérés — jamais de crash.
/// Implémente le contrat <see cref="IRefData"/> exposé aux modules.
/// </summary>
public sealed class RefData : IRefData
{
    private readonly Dictionary<string, string> _speciesNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _passiveNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _itemNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpeciesInfo> _species = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<DropSource>> _dropSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RecipeInfo> _recipes = [];
    private readonly List<SpecialCombo> _specialCombos = [];
    private readonly Dictionary<string, List<SpawnSpot>> _spawnSpots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Bornes monde (min/max X,Y) de tous les emplacements de spawn — pour cadrer la carte.</summary>
    public (double MinX, double MaxX, double MinY, double MaxY) SpawnBounds { get; private set; }

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

    public IReadOnlyList<SpawnSpot> SpawnSpotsFor(string speciesId) =>
        _spawnSpots.TryGetValue(speciesId, out var list) ? list : [];

    public bool HasSpawnData => _spawnSpots.Count > 0;

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
            refData.LoadSpawners(Path.Combine(dir, "spawners.json"));
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
            var materials = new List<RecipeMaterial>();
            if (value.TryGetProperty("materials", out var mats))
            {
                foreach (var material in mats.EnumerateArray())
                {
                    materials.Add(new RecipeMaterial(material.GetProperty("item_id").GetString() ?? "",
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

    private void LoadSpawners(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        // Emplacements indexés par nom de spawner.
        var placementsByName = new Dictionary<string, List<(double X, double Y, double Z, double Radius)>>(StringComparer.OrdinalIgnoreCase);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var placement in doc.RootElement.GetProperty("placements").EnumerateArray())
        {
            var name = placement.GetProperty("spawner_name").GetString() ?? "";
            if (name.Length == 0)
            {
                continue;
            }
            var x = placement.GetProperty("x").GetDouble();
            var y = placement.GetProperty("y").GetDouble();
            if (!placementsByName.TryGetValue(name, out var list))
            {
                list = [];
                placementsByName[name] = list;
            }
            list.Add((x, y,
                      placement.GetProperty("z").GetDouble(),
                      placement.GetProperty("radius").GetDouble()));
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
        if (minX < maxX)
        {
            SpawnBounds = (minX, maxX, minY, maxY);
        }

        // Groupes (loterie pondérée par spawner_name) -> spots par espèce.
        foreach (var group in doc.RootElement.GetProperty("spawn_groups").EnumerateObject())
        {
            var value = group.Value;
            var spawnerName = value.GetProperty("spawner_name").GetString() ?? "";
            if (!placementsByName.TryGetValue(spawnerName, out var placements))
            {
                continue; // ~5 % de groupes sans emplacement connu
            }
            var onlyTime = value.TryGetProperty("only_time", out var t) ? t.GetInt32() : 0;
            foreach (var pal in value.GetProperty("pals").EnumerateArray())
            {
                var speciesId = pal.GetProperty("species_id").GetString() ?? "";
                if (speciesId.Length == 0)
                {
                    continue;
                }
                var levelMin = pal.GetProperty("level_min").GetInt32();
                var levelMax = pal.GetProperty("level_max").GetInt32();
                if (!_spawnSpots.TryGetValue(speciesId, out var spots))
                {
                    spots = [];
                    _spawnSpots[speciesId] = spots;
                }
                foreach (var (x, y, z, radius) in placements)
                {
                    spots.Add(new SpawnSpot(x, y, z, radius, onlyTime, levelMin, levelMax));
                }
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
