using System.Text.Json;

namespace Overkit.Host.Cards;

/// <summary>Une saisie déclarée par la Card en cours d'édition, telle qu'un filtre peut la viser.</summary>
public sealed record CardInputRef(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Un filtre choisi dans l'éditeur : champ, opérateur, et soit une valeur
/// fixe, soit une saisie de la Card — auquel cas le filtre suit ce que le
/// joueur tape, au lieu d'être figé à la création.
/// </summary>
public sealed record CardFilter(CardField Field, string Operator, string Value, CardInputRef? Input = null)
{
    /// <summary>Traduit le choix en expression du langage des Cards.</summary>
    public string ToExpression()
    {
        if (Input is not null)
        {
            return $"{Field.Path} {Operator} inputs.{Input.Id}";
        }
        var literal = Field.Kind == CardFieldKind.Number && double.TryParse(Value, out _)
            ? Value
            : $"\"{Value.Replace("\"", "")}\"";
        return $"{Field.Path} {Operator} {literal}";
    }

    public string ToLabel()
    {
        var op = CardFieldCatalog.Operators.FirstOrDefault(o => o.Symbol == Operator).Label ?? Operator;
        return Input is not null
            ? $"{Field.Label} {op} la saisie « {Input.Label} »"
            : $"{Field.Label} {op} {Value}";
    }
}

/// <summary>
/// Construit une définition de Card à partir des choix faits dans l'éditeur
/// in-game (§5.1). C'est ici que les sélections deviennent des expressions :
/// le créateur n'écrit jamais de code.
/// </summary>
public static class CardBuilder
{
    /// <summary>« palbox.pals | where(level >= 40) », ou la source seule sans filtre.</summary>
    public static string BuildSourceExpression(CardSource source, IReadOnlyList<CardFilter> filters)
    {
        if (filters.Count == 0)
        {
            return source.Expression;
        }
        var condition = string.Join(" and ", filters.Select(f => f.ToExpression()));
        return $"{source.Expression} | where({condition})";
    }

    public static CardSection BuildCounter(string label, CardSource source, IReadOnlyList<CardFilter> filters) =>
        new()
        {
            Type = "counters",
            Items = [new CardItem { Label = label, Value = $"count({BuildSourceExpression(source, filters)})" }],
        };

    public static CardSection BuildGlobalCounter(string label, CardField field) =>
        new()
        {
            Type = "counters",
            Items = [new CardItem { Label = label, Value = field.Path }],
        };

    public static CardSection BuildList(CardSource source, IReadOnlyList<CardFilter> filters,
                                        IReadOnlyList<CardField> columns, CardField? sortBy,
                                        bool sortDescending, int limit) =>
        new()
        {
            Type = "list",
            Source = BuildSourceExpression(source, filters),
            Columns = columns.Select(c => new CardColumn { Header = c.Label.ToUpperInvariant(), Value = c.Path }).ToList(),
            SortBy = sortBy?.Path,
            SortDescending = sortDescending,
            Limit = limit,
            EmptyText = $"Aucun résultat parmi mes {source.ItemNoun}.",
        };

    public static CardSection BuildAlert(string title, CardSource source, IReadOnlyList<CardFilter> filters,
                                         string level)
    {
        var expression = BuildSourceExpression(source, filters);
        return new CardSection
        {
            Type = "alert",
            When = $"count({expression}) > 0",
            Level = level,
            Title = title,
            Detail = $"{{count({expression})}} {source.ItemNoun} concerné(s)",
        };
    }

    public static CardSection BuildText(string text) => new() { Type = "text", Text = text };

    /// <summary>
    /// Section de saisie. L'éditeur ne demande pas d'identifiant : il le
    /// dérive de l'intitulé, puisque c'est ce que le créateur a en tête quand
    /// il vise la saisie depuis un filtre.
    /// </summary>
    public static CardSection BuildInput(string label, string kind, string defaultValue,
                                         IReadOnlyList<string> options)
    {
        var section = new CardSection { Id = InputId(label), Label = label, Type = kind };

        switch (kind)
        {
            case "number":
                section.Min = 0;
                section.Max = 9999;
                section.Step = 1;
                section.Default = JsonDocument.Parse(
                    double.TryParse(defaultValue, out var number)
                        ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : "0").RootElement.Clone();
                break;

            case "toggle":
                section.Default = JsonDocument
                    .Parse(defaultValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false")
                    .RootElement.Clone();
                break;

            case "choice":
                section.Options = options.Select(o => new CardOption { Value = o, Label = o }).ToList();
                section.Default = JsonSerializer.SerializeToElement(
                    options.Contains(defaultValue) ? defaultValue : options.FirstOrDefault() ?? "");
                break;

            default:
                section.Type = "input";
                section.Placeholder = "laisser vide pour ne pas filtrer";
                section.Default = JsonSerializer.SerializeToElement(defaultValue);
                break;
        }

        return section;
    }

    /// <summary>
    /// Nom de variable pour une saisie : c'est lui qui apparaît dans les
    /// expressions sous « inputs.x », il doit donc rester un identifiant
    /// valide même si l'intitulé est accentué ou commence par un chiffre.
    /// </summary>
    public static string InputId(string label)
    {
        var id = Slugify(label).Replace('-', '_');
        return id.Length == 0 || char.IsDigit(id[0]) ? "saisie_" + id : id;
    }

    /// <summary>Saisies déclarées par une suite de blocs, dans l'ordre d'affichage.</summary>
    public static List<CardInputRef> InputsOf(IEnumerable<CardSection> sections) =>
        sections
            .Where(s => s.Id is { Length: > 0 } && InputTypes.Contains(s.Type.ToLowerInvariant()))
            .Select(s => new CardInputRef(s.Id!, s.Label is { Length: > 0 } label ? label : s.Id!))
            .ToList();

    private static readonly string[] InputTypes = ["input", "number", "choice", "toggle"];

    public static CardDefinition BuildCard(string name, IEnumerable<CardSection> sections, string author)
    {
        var slug = Slugify(name);
        return new CardDefinition
        {
            // Préfixe « local » plutôt qu'un TLD réel : la card est créée dans
            // l'éditeur, on ne connaît aucun domaine appartenant au joueur. Un
            // identifiant publié au registre sera renommé en reverse-DNS.
            Id = $"local.{(string.IsNullOrWhiteSpace(author) ? "joueur" : Slugify(author))}.{slug}",
            Name = name,
            Version = "1.0.0",
            Authors = string.IsNullOrWhiteSpace(author) ? [] : [author],
            License = "",
            StateRequires = sections
                .Select(DomainOf)
                .Where(d => d is not null)
                .Distinct()
                .Cast<string>()
                .ToList(),
            Sections = sections.ToList(),
        };
    }

    /// <summary>Domaine du State Bus requis par une section, déduit de sa source.</summary>
    private static string? DomainOf(CardSection section)
    {
        var haystack = (section.Source ?? "") + (section.When ?? "") +
                       string.Join(" ", section.Items.Select(i => $"{i.Value}{i.Current}{i.Max}"));
        return CardFieldCatalog.Sources
            .FirstOrDefault(s => haystack.Contains(s.Expression, StringComparison.OrdinalIgnoreCase))?.Domain;
    }

    /// <summary>
    /// Décrit une section existante en une ligne lisible, pour réafficher les
    /// blocs quand on rouvre une Card dans l'éditeur.
    /// </summary>
    public static string Describe(CardSection section)
    {
        var source = CardFieldCatalog.Sources.FirstOrDefault(s =>
            (section.Source ?? section.When ?? "").Contains(s.Expression, StringComparison.OrdinalIgnoreCase));
        var among = source is null ? "" : $" — {source.Label}";

        return section.Type.ToLowerInvariant() switch
        {
            "counters" => "Compteur — " + string.Join(", ", section.Items.Select(i => i.Label)),
            "gauges" => "Jauges — " + string.Join(", ", section.Items.Select(i => i.Label)),
            "list" => $"Liste{among} ({section.Columns.Count} colonne(s))",
            "alert" => $"Alerte — {section.Title}",
            "text" => $"Texte — {section.Text}",
            "input" => $"Saisie texte — {section.Label}",
            "number" => $"Saisie nombre — {section.Label}",
            "choice" => $"Choix — {section.Label} ({section.Options.Count} option(s))",
            "toggle" => $"Oui/non — {section.Label}",
            _ => section.Type,
        };
    }

    public static string Serialize(CardDefinition card) =>
        JsonSerializer.Serialize(card, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

    /// <summary>Nom de fichier sûr et lisible pour la Card.</summary>
    public static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace('é', 'e').Replace('è', 'e').Replace('ê', 'e').Replace('à', 'a')
            .Replace('ù', 'u').Replace('ô', 'o').Replace('î', 'i').Replace('ç', 'c');
        var chars = normalized.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        slug = slug.Trim('-');
        return slug.Length == 0 ? "ma-card" : slug;
    }
}
