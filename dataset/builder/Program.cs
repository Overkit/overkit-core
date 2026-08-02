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

Console.WriteLine($"OK : {pals.Count} espèces, {passives.Count} passifs nommés, {combos.Count} combos spéciaux");
return 0;
