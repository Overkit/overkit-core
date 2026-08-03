using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overkit.Host.Cards;

/// <summary>
/// Définition d'une Card (§5.1) : un fichier JSON unique, partageable tel quel
/// (EXG-041). Le créateur choisit des sections parmi les templates fournis et
/// les lie aux champs du State Bus par des expressions — sans écrire de code.
/// </summary>
public sealed class CardDefinition
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
    [JsonPropertyName("authors")] public List<string> Authors { get; set; } = [];
    [JsonPropertyName("license")] public string License { get; set; } = "";
    [JsonPropertyName("state_requires")] public List<string> StateRequires { get; set; } = [];
    [JsonPropertyName("min_schema")] public string MinSchema { get; set; } = "1.0";
    [JsonPropertyName("sections")] public List<CardSection> Sections { get; set; } = [];

    public static CardDefinition Parse(string json) =>
        JsonSerializer.Deserialize<CardDefinition>(json, Options)
        ?? throw new InvalidOperationException("card vide");

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>Une section de Card. `type` choisit le template de rendu.</summary>
public sealed class CardSection
{
    /// <summary>text | counters | gauges | list | alert</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "text";

    /// <summary>Texte libre ou expression selon le template.</summary>
    [JsonPropertyName("text")] public string? Text { get; set; }

    /// <summary>Affiche la section seulement si l'expression est vraie.</summary>
    [JsonPropertyName("when")] public string? When { get; set; }

    // counters / gauges
    [JsonPropertyName("items")] public List<CardItem> Items { get; set; } = [];

    // list
    [JsonPropertyName("source")] public string? Source { get; set; }
    [JsonPropertyName("columns")] public List<CardColumn> Columns { get; set; } = [];
    [JsonPropertyName("limit")] public int Limit { get; set; } = 100;
    [JsonPropertyName("sort_by")] public string? SortBy { get; set; }
    [JsonPropertyName("sort_desc")] public bool SortDescending { get; set; }
    [JsonPropertyName("empty_text")] public string? EmptyText { get; set; }

    // alert
    [JsonPropertyName("level")] public string Level { get; set; } = "warning";
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("detail")] public string? Detail { get; set; }

    /// <summary>Pour une alerte par élément : chaque item de `source` produit une alerte si `when` est vrai.</summary>
    [JsonPropertyName("for_each")] public bool ForEach { get; set; }
}

public sealed class CardItem
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";

    /// <summary>Expression — counters.</summary>
    [JsonPropertyName("value")] public string? Value { get; set; }

    /// <summary>Expressions — gauges.</summary>
    [JsonPropertyName("current")] public string? Current { get; set; }
    [JsonPropertyName("max")] public string? Max { get; set; }
    [JsonPropertyName("warn_below")] public double? WarnBelow { get; set; }
}

public sealed class CardColumn
{
    [JsonPropertyName("header")] public string Header { get; set; } = "";
    [JsonPropertyName("value")] public string Value { get; set; } = "";
}
