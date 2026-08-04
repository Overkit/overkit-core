namespace Overkit.Host.Cards;

/// <summary>Un champ exploitable dans l'éditeur de Cards, avec son libellé lisible.</summary>
public sealed record CardField(string Path, string Label, CardFieldKind Kind, string[]? Choices = null);

public enum CardFieldKind
{
    Number,
    Text,
    Choice,
}

/// <summary>Une source de données : une collection du State Bus et ses champs.</summary>
public sealed record CardSource(
    string Key,
    string Label,
    string Expression,
    string Domain,
    IReadOnlyList<CardField> Fields,
    string ItemNoun);

/// <summary>
/// Catalogue des sources et champs proposés par l'éditeur de Cards in-game.
/// Il évite au créateur d'écrire des chemins à la main : il choisit dans des
/// listes, l'éditeur génère l'expression. Ajouter une entrée ici suffit à
/// l'exposer dans l'éditeur.
/// </summary>
public static class CardFieldCatalog
{
    public static readonly IReadOnlyList<CardSource> Sources =
    [
        new("pals", "Mes Pals", "palbox.pals", "palbox",
        [
            new CardField("species_id", "Espèce (identifiant)", CardFieldKind.Text),
            new CardField("nickname", "Surnom", CardFieldKind.Text),
            new CardField("level", "Niveau", CardFieldKind.Number),
            new CardField("gender", "Genre", CardFieldKind.Choice, ["male", "female", "unknown"]),
            new CardField("talents.hp", "Talent PV", CardFieldKind.Number),
            new CardField("talents.melee", "Talent mêlée", CardFieldKind.Number),
            new CardField("talents.shot", "Talent tir", CardFieldKind.Number),
            new CardField("talents.defense", "Talent défense", CardFieldKind.Number),
        ], "Pals"),

        new("bases", "Mes bases", "bases.list", "bases",
        [
            new CardField("base_id", "Identifiant", CardFieldKind.Text),
            new CardField("count(workers)", "Nombre de travailleurs", CardFieldKind.Number),
        ], "bases"),

        new("workers", "Les travailleurs d'une base", "workers", "bases",
        [
            new CardField("percent(hunger.current, hunger.max)", "Faim (%)", CardFieldKind.Number),
            new CardField("percent(sanity.current, sanity.max)", "Santé mentale (%)", CardFieldKind.Number),
            new CardField("instance_id", "Identifiant", CardFieldKind.Text),
        ], "travailleurs"),

        new("nearby", "Pals autour de moi", "nearby.actors", "nearby",
        [
            new CardField("species_id", "Espèce (identifiant)", CardFieldKind.Text),
            new CardField("level", "Niveau", CardFieldKind.Number),
            new CardField("distance", "Distance (cm)", CardFieldKind.Number),
        ], "Pals proches"),
    ];

    /// <summary>Valeurs scalaires affichables directement (compteurs, textes).</summary>
    public static readonly IReadOnlyList<CardField> Globals =
    [
        new CardField("palbox.owned_count", "Pals possédés (total)", CardFieldKind.Number),
        new CardField("world.time.day", "Jour in-game", CardFieldKind.Number),
        new CardField("concat(pad(world.time.hour, 2), \":\", pad(world.time.minute, 2))",
                      "Heure in-game (hh:mm)", CardFieldKind.Text),
        new CardField("world.time.hour", "Heure in-game (0-23)", CardFieldKind.Number),
        new CardField("world.time.minute", "Minute in-game", CardFieldKind.Number),
        new CardField("count(bases.list)", "Nombre de bases", CardFieldKind.Number),
        new CardField("count(nearby.actors)", "Pals autour de moi", CardFieldKind.Number),
    ];

    public static readonly IReadOnlyList<(string Symbol, string Label)> Operators =
    [
        ("=", "est égal à"),
        ("!=", "est différent de"),
        (">", "est supérieur à"),
        (">=", "est supérieur ou égal à"),
        ("<", "est inférieur à"),
        ("<=", "est inférieur ou égal à"),
    ];

    public static CardSource? FindSource(string key) =>
        Sources.FirstOrDefault(s => s.Key == key);
}
