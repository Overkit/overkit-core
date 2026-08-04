using System.Text.Json;

namespace Overkit.Host.Cards;

/// <summary>Un filtre choisi dans l'éditeur : champ, opérateur, valeur.</summary>
public sealed record CardFilter(CardField Field, string Operator, string Value)
{
    /// <summary>Traduit le choix en expression du langage des Cards.</summary>
    public string ToExpression()
    {
        var literal = Field.Kind == CardFieldKind.Number && double.TryParse(Value, out _)
            ? Value
            : $"\"{Value.Replace("\"", "")}\"";
        return $"{Field.Path} {Operator} {literal}";
    }

    public string ToLabel() =>
        $"{Field.Label} {CardFieldCatalog.Operators.FirstOrDefault(o => o.Symbol == Operator).Label ?? Operator} {Value}";
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

    public static CardDefinition BuildCard(string name, IEnumerable<CardSection> sections, string author)
    {
        var slug = Slugify(name);
        return new CardDefinition
        {
            Id = $"com.{(string.IsNullOrWhiteSpace(author) ? "joueur" : Slugify(author))}.{slug}",
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
