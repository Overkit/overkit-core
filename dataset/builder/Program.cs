// Builder du dataset Overkit (§4.1) : post-traite les dumps bruts du Dumper
// (JSON par table) en fichiers de dataset propres consommés par le host et
// les modules. Usage :
//   dotnet run --project dataset/builder -- <dossier_raw> <dossier_out>
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage : builder <dossier_raw> <dossier_out>");
    return 1;
}

var rawDir = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);

JsonObject LoadRows(string table)
{
    var path = Path.Combine(rawDir, table + ".json");
    var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    return root["rows"]!.AsObject();
}

string? Str(JsonNode? node) => node?.GetValue<string>();

// --- Tables sources ---
var palNames = LoadRows("DT_PalNameText");
var monster = LoadRows("DT_PalMonsterParameter");
var combiUnique = LoadRows("DT_PalCombiUnique");
var skillNames = LoadRows("DT_SkillNameText");
var passivesMain = LoadRows("DT_PassiveSkill_Main");

string Prettify(string id) =>
    Regex.Replace(id.Replace('_', ' ').Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ");

// Un texte localisé absent ou placeholder (« en_text », « fr_Text »…) n'est
// pas un nom utilisable.
bool IsRealText(string? text) =>
    !string.IsNullOrWhiteSpace(text) && !Regex.IsMatch(text, "^[a-z]{2}_[Tt]ext$");

string? LocalizedName(JsonObject table, string key) =>
    table[key] is JsonObject row && Str(row["TextData"]) is { } text && IsRealText(text) ? text : null;

// --- pals.json ---
var pals = new JsonObject();
var tribeToSpecies = new Dictionary<int, (string Key, int NameLen)>();
foreach (var (key, row) in monster)
{
    var r = row!.AsObject();
    if (r["IsPal"]?.GetValue<bool>() != true)
    {
        continue;
    }

    var overrideName = Str(r["OverrideNameTextID"]);
    var nameId = overrideName is not null and not "None" ? overrideName : "PAL_NAME_" + key;
    var name = LocalizedName(palNames, nameId) ?? Prettify(key);

    pals[key] = new JsonObject
    {
        ["name"] = name,
        ["zukan_index"] = r["ZukanIndex"]?.GetValue<int>() ?? -1,
        ["zukan_suffix"] = Str(r["ZukanIndexSuffix"]) ?? "",
        ["element_types"] = new JsonArray(r["ElementType1"]?.GetValue<int>() ?? 0,
                                          r["ElementType2"]?.GetValue<int>() ?? 0),
        ["combi_rank"] = r["CombiRank"]?.GetValue<int>() ?? 0,
        ["rarity"] = r["Rarity"]?.GetValue<int>() ?? 0,
        ["tribe"] = r["Tribe"]?.GetValue<int>() ?? -1,
    };

    // Table tribu -> espèce de base : parmi les lignes d'une même tribu
    // (variantes BOSS_/RAID_ incluses), la clé la plus courte au Zukan
    // valide est l'espèce de base.
    var tribe = r["Tribe"]?.GetValue<int>() ?? -1;
    var zukan = r["ZukanIndex"]?.GetValue<int>() ?? -1;
    if (tribe >= 0 && zukan > 0 &&
        (!tribeToSpecies.TryGetValue(tribe, out var existing) || key.Length < existing.NameLen))
    {
        tribeToSpecies[tribe] = (key, key.Length);
    }
}

// --- passives.json ---
var passives = new JsonObject();
foreach (var (key, row) in passivesMain)
{
    var name = LocalizedName(skillNames, "PASSIVE_" + key);
    if (name is null)
    {
        continue; // passif interne/placeholder, inutile à l'affichage
    }
    passives[key] = new JsonObject
    {
        ["name"] = name,
        ["rank"] = row!.AsObject()["Rank"]?.GetValue<int>() ?? 0,
    };
}

// --- breeding.json (combos spéciaux ; la formule CombiRank est dans pals.json) ---
var combos = new JsonArray();
foreach (var (_, row) in combiUnique)
{
    var r = row!.AsObject();
    var tribeA = r["ParentTribeA"]?.GetValue<int>() ?? -1;
    var tribeB = r["ParentTribeB"]?.GetValue<int>() ?? -1;
    combos.Add(new JsonObject
    {
        ["parent_a"] = tribeToSpecies.TryGetValue(tribeA, out var a) ? a.Key : $"tribe:{tribeA}",
        ["parent_gender_a"] = r["ParentGenderA"]?.GetValue<int>() ?? 0,
        ["parent_b"] = tribeToSpecies.TryGetValue(tribeB, out var b) ? b.Key : $"tribe:{tribeB}",
        ["parent_gender_b"] = r["ParentGenderB"]?.GetValue<int>() ?? 0,
        ["child"] = Str(r["ChildCharacterID"]) ?? "",
    });
}

double Num(JsonNode? node) => node?.GetValue<double>() ?? 0;

// --- items.json (noms d'objets localisés) ---
var itemNames = LoadRows("DT_ItemNameText");
var items = new JsonObject();
foreach (var (key, row) in itemNames)
{
    if (!key.StartsWith("ITEM_NAME_"))
    {
        continue;
    }
    var id = key["ITEM_NAME_".Length..];
    if (Str(row!.AsObject()["TextData"]) is { } text && IsRealText(text))
    {
        items[id] = new JsonObject { ["name"] = text };
    }
}

// --- recipes.json ---
var recipeRows = LoadRows("DT_ItemRecipeDataTable");
var recipes = new JsonObject();
foreach (var (key, row) in recipeRows)
{
    var r = row!.AsObject();
    var materials = new JsonArray();
    for (var m = 1; m <= 5; m++)
    {
        var id = Str(r[$"Material{m}_Id"]);
        if (id is null or "None")
        {
            continue;
        }
        materials.Add(new JsonObject { ["item_id"] = id, ["count"] = (int)Num(r[$"Material{m}_Count"]) });
    }
    recipes[key] = new JsonObject
    {
        ["product_id"] = Str(r["Product_Id"]) ?? key,
        ["product_count"] = (int)Num(r["Product_Count"]),
        ["work_amount"] = Num(r["WorkAmount"]),
        ["unlock_item_id"] = Str(r["UnlockItemID"]) is { } u and not "None" ? u : null,
        ["materials"] = materials,
    };
}

// --- drops.json (agrégés par espèce) ---
var dropRows = LoadRows("DT_PalDropItem");
var drops = new JsonObject();
foreach (var (_, row) in dropRows)
{
    var r = row!.AsObject();
    var character = Str(r["CharacterID"]);
    if (string.IsNullOrEmpty(character))
    {
        continue;
    }
    if (drops[character] is not JsonArray list)
    {
        list = [];
        drops[character] = list;
    }
    for (var d = 1; d <= 10; d++)
    {
        var id = Str(r[$"ItemId{d}"]);
        if (id is null or "None")
        {
            continue;
        }
        list.Add(new JsonObject
        {
            ["item_id"] = id,
            ["rate"] = Num(r[$"Rate{d}"]),
            ["min"] = (int)Num(r[$"min{d}"]),
            ["max"] = (int)Num(r[$"Max{d}"]),
        });
    }
}

// --- spawners.json : groupes (qui spawne, quand) + emplacements (où) ---
// Jointure groupes<->placements par SpawnerName à affiner (conventions de
// nommage différentes selon les zones) — les deux jeux sont émis bruts.
var wildRows = LoadRows("DT_PalWildSpawner");
var spawnGroups = new JsonObject();
foreach (var (key, row) in wildRows)
{
    var r = row!.AsObject();
    var groupPals = new JsonArray();
    for (var p = 1; p <= 3; p++)
    {
        var id = Str(r[$"Pal_{p}"]);
        if (id is null or "None" or "RowName")
        {
            continue;
        }
        groupPals.Add(new JsonObject
        {
            ["species_id"] = id,
            ["level_min"] = (int)Num(r[$"LvMin_{p}"]),
            ["level_max"] = (int)Num(r[$"LvMax_{p}"]),
            ["num_min"] = (int)Num(r[$"NumMin_{p}"]),
            ["num_max"] = (int)Num(r[$"NumMax_{p}"]),
        });
    }
    if (groupPals.Count == 0)
    {
        continue;
    }
    spawnGroups[key] = new JsonObject
    {
        ["spawner_name"] = Str(r["SpawnerName"]) ?? key,
        ["weight"] = Num(r["Weight"]),
        ["only_time"] = (int)Num(r["OnlyTime"]),
        ["only_weather"] = (int)Num(r["OnlyWeather"]),
        ["pals"] = groupPals,
    };
}

var placementRows = LoadRows("DT_PalSpawnerPlacement");
var placements = new JsonArray();
foreach (var (_, row) in placementRows)
{
    var r = row!.AsObject();
    if (r["Location"] is not JsonObject location)
    {
        continue;
    }
    placements.Add(new JsonObject
    {
        ["spawner_name"] = Str(r["SpawnerName"]) ?? "",
        ["x"] = Num(location["X"]),
        ["y"] = Num(location["Y"]),
        ["z"] = Num(location["Z"]),
        ["radius"] = Num(r["StaticRadius"]),
        ["world"] = Str(r["WorldName"]) ?? "",
        ["type"] = (int)Num(r["SpawnerType"]),
    });
}

var meta = new JsonObject
{
    ["schema_version"] = "0.1-draft",
    ["generated_at"] = DateTime.UtcNow.ToString("O"),
    ["game_build"] = "1.10.1103.0",
};

void Write(string file, JsonObject payload)
{
    payload.Insert(0, "$meta", meta.DeepClone());
    File.WriteAllText(Path.Combine(outDir, file),
        payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    Console.WriteLine($"{file} écrit");
}

Write("pals.json", new JsonObject { ["pals"] = pals });
Write("passives.json", new JsonObject { ["passives"] = passives });
Write("breeding.json", new JsonObject { ["special_combos"] = combos });
Write("items.json", new JsonObject { ["items"] = items });
Write("recipes.json", new JsonObject { ["recipes"] = recipes });
Write("drops.json", new JsonObject { ["drops"] = drops });
Write("spawners.json", new JsonObject { ["spawn_groups"] = spawnGroups, ["placements"] = placements });

Console.WriteLine($"OK : {pals.Count} espèces, {passives.Count} passifs, {combos.Count} combos, " +
                  $"{items.Count} objets nommés, {recipes.Count} recettes, {drops.Count} espèces avec drops, " +
                  $"{spawnGroups.Count} groupes de spawn, {placements.Count} emplacements");
return 0;
