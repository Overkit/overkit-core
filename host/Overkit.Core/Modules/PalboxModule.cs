using Overkit.Contracts;
using Overkit.Sdk;

namespace Overkit.Host.Modules;

/// <summary>
/// Vue Palbox (§6.4) : la boîte complète, cherchable et triable. Écrite comme
/// un module déclaratif et non en WinUI — elle passe donc par le même contrat
/// qu'un module tiers, ce qui garantit qu'un tiers peut en écrire l'équivalent.
/// </summary>
public sealed class PalboxModule : IOverkitModule
{
    private IModuleContext _context = null!;
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;

    private string _search = "";
    private string _sort = SortByLevel;

    private const string SortByLevel = "level";
    private const string SortByName = "name";
    private const string SortByTalents = "talents";

    public ModuleManifest Manifest { get; } = new()
    {
        Id = "fr.overkit.palbox",
        Name = "Palbox",
        Version = "1.0.0",
        Authors = ["Nallraen"],
        License = "MIT",
        Homepage = "https://github.com/Overkit/overkit",
        StateRequires = ["palbox"],
        StateOptional = ["party"],
        Capabilities = ["refdata"],
        MinSchema = "1.0",
    };

    public void Initialize(IModuleContext context) => _context = context;

    public void OnStateUpdated(GameStateSnapshot snapshot) => _snapshot = snapshot;

    public void OnInteraction(ViewInteraction interaction)
    {
        switch (interaction.Id)
        {
            case "search":
                _search = interaction.Value.Trim();
                break;
            case "sort":
                _sort = interaction.Value;
                break;
        }
    }

    public ModuleView BuildView()
    {
        var snapshot = _snapshot;
        var palbox = snapshot.Palbox;

        if (palbox?.Pals is null || palbox.Status == FieldStatus.Unavailable)
        {
            return new ModuleView(Manifest.Name, [
                new EmptySection(snapshot.Mode == ConnectionMode.Static
                    ? "Données live indisponibles — la Sonde n'est pas connectée."
                    : "En attente de la Sonde…"),
            ]);
        }

        // Une Palbox partiellement lue n'est pas une Palbox vide : le dire,
        // sinon le joueur croit avoir perdu des Pals.
        var status = palbox.Status == FieldStatus.Degraded && palbox.Owned_count is > 0
            ? $"{palbox.Pals.Count}/{palbox.Owned_count} synchronisés — ouvrir la boîte en jeu pour compléter"
            : $"{palbox.Pals.Count} Pals";

        var party = snapshot.Party?.Member_instance_ids is { Count: > 0 } ids
            ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
            : [];

        var rows = palbox.Pals
            .Select(pal => Describe(pal, party.Contains(pal.Instance_id)))
            .Where(Matches)
            .ToList();

        rows = _sort switch
        {
            SortByName => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            SortByTalents => rows.OrderByDescending(r => r.TalentTotal).ThenByDescending(r => r.Level).ToList(),
            _ => rows.OrderByDescending(r => r.Level)
                     .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                     .ToList(),
        };

        var controls = new ViewSection[]
        {
            new StatusSection(status),
            new TextInputSection("search", "", _search, "Rechercher un Pal, une espèce, un passif…"),
            new ChoiceSection("sort", "Trier par", [
                new ChoiceOption(SortByLevel, "Niveau"),
                new ChoiceOption(SortByName, "Nom"),
                new ChoiceOption(SortByTalents, "Total des talents"),
            ], _sort),
        };

        if (rows.Count == 0)
        {
            return new ModuleView(Manifest.Name, [
                ..controls,
                new EmptySection(_search.Length > 0
                    ? $"Aucun Pal ne correspond à « {_search} »."
                    : "La boîte est vide."),
            ]);
        }

        return new ModuleView(Manifest.Name, [
            ..controls,
            new TableSection(["PAL", "NIVEAU", "TALENTS (IV)", "PASSIFS"],
                rows.Select(r => new TableRow([
                    new TableCell(r.Glyph + " " + r.Display, null, r.SubTitle),
                    r.Level > 0 ? $"Nv {r.Level}" : "—",
                    r.Talents,
                    r.Passives,
                ])).ToList()),
        ]);
    }

    private bool Matches(PalDescription pal) =>
        _search.Length == 0 || pal.SearchText.Contains(_search, StringComparison.OrdinalIgnoreCase);

    private PalDescription Describe(Pal pal, bool inParty)
    {
        var refData = _context.RefData;
        var species = refData?.SpeciesName(pal.Species_id) ?? pal.Species_id;
        var nickname = string.IsNullOrWhiteSpace(pal.Nickname) ? null : pal.Nickname;
        var passives = pal.Passives is { Count: > 0 } list
            ? string.Join("  ·  ", list.Select(p => refData?.PassiveName(p) ?? p))
            : "—";

        var talents = pal.Talents;
        var total = (talents?.Hp ?? 0) + (talents?.Melee ?? 0) + (talents?.Shot ?? 0) + (talents?.Defense ?? 0);

        return new PalDescription(
            Display: (inParty ? "★ " : "") + (nickname ?? species),
            SubTitle: nickname is null ? (inParty ? "dans l'équipe" : "") : species,
            Glyph: pal.Gender switch
            {
                PalGender.Male => "♂",
                PalGender.Female => "♀",
                _ => "•",
            },
            Name: nickname ?? species,
            Level: pal.Level ?? 0,
            Talents: $"PV {Show(talents?.Hp)}   MÊL {Show(talents?.Melee)}   " +
                     $"TIR {Show(talents?.Shot)}   DÉF {Show(talents?.Defense)}",
            TalentTotal: total,
            Passives: passives,
            SearchText: $"{nickname} {species} {pal.Species_id} {passives}");

        static string Show(int? value) => value?.ToString() ?? "?";
    }

    private sealed record PalDescription(
        string Display,
        string SubTitle,
        string Glyph,
        string Name,
        int Level,
        string Talents,
        int TalentTotal,
        string Passives,
        string SearchText);
}
