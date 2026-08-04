using Overkit.Sdk;

namespace MyOverkitModule;

/// <summary>
/// Point d'entrée du module : l'overlay l'instancie au démarrage, lui pousse
/// un snapshot immuable à chaque mise à jour de l'état du jeu, et lui demande
/// une vue à afficher. Un module ne peut rien écrire dans le jeu et ne crée
/// aucune fenêtre : il décrit, l'overlay rend.
/// </summary>
public sealed class Module : IOverkitModule
{
    private IModuleContext _context = null!;
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;

    // État propre au module, piloté par les sections interactives de la vue.
    private string _search = "";

    public ModuleManifest Manifest { get; } = new()
    {
        // Reverse-DNS sur un domaine contrôlé : c'est ce qui garantit l'unicité
        // de l'identifiant face aux autres modules chargés.
        Id = "MODULE_ID_PREFIX.MODULE_AUTHOR_SLUG.MODULE_SLUG",
        Name = "MODULE_DISPLAY_NAME",
        Version = "1.0.0",
        Authors = ["MODULE_AUTHOR"],
        License = "MIT",

        // Domaines du State Bus nécessaires : sans eux, l'overlay désactive le
        // module avec un message plutôt que d'afficher des données fausses.
        StateRequires = ["palbox"],
        StateOptional = [],

        // « refdata » donne accès au dataset (noms localisés, recettes,
        // spawners…). Sans capacité déclarée, Context.RefData vaut null.
        Capabilities = ["refdata"],
        MinSchema = "1.0",
    };

    public void Initialize(IModuleContext context) => _context = context;

    /// <summary>
    /// Appelé à chaque snapshot. Doit rester bref : pas d'entrée/sortie, pas
    /// d'attente. Le calcul lourd a sa place dans BuildView, appelé seulement
    /// quand l'onglet est visible.
    /// </summary>
    public void OnStateUpdated(GameStateSnapshot snapshot) => _snapshot = snapshot;

    /// <summary>
    /// Reçoit ce que l'utilisateur saisit dans les sections interactives, par
    /// identifiant de section. L'overlay redemande la vue juste après.
    /// </summary>
    public void OnInteraction(ViewInteraction interaction)
    {
        if (interaction.Id == "search")
        {
            _search = interaction.Value;
        }
    }

    public ModuleView BuildView()
    {
        var palbox = _snapshot.Palbox;

        if (palbox?.Pals is not { Count: > 0 } pals)
        {
            return new ModuleView(Manifest.Name, [
                new EmptySection(_snapshot.Mode == ConnectionMode.Static
                    ? "Données live indisponibles — la sonde n'est pas connectée."
                    : "En attente des données de la Palbox."),
            ]);
        }

        // Exemple : répartition par genre et cinq Pals de plus haut niveau.
        var males = pals.Count(p => p.Gender == Overkit.Contracts.PalGender.Male);
        var females = pals.Count(p => p.Gender == Overkit.Contracts.PalGender.Female);

        var rows = pals
            .Select(p => (Pal: p, Name: _context.RefData?.SpeciesName(p.Species_id) ?? p.Species_id))
            .Where(x => _search.Length == 0 || x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Pal.Level ?? 0)
            .Take(5)
            .Select(x => new TableRow([x.Name, (x.Pal.Level ?? 0).ToString()]))
            .ToList();

        return new ModuleView(Manifest.Name, [
            new StatusSection($"{pals.Count} Pals dans la boîte"),
            new CountersSection([
                new CounterItem("Mâles", males.ToString()),
                new CounterItem("Femelles", females.ToString()),
            ]),
            new TextInputSection("search", "Rechercher", _search, "nom d'espèce"),
            new TableSection(["PAL", "NIVEAU"], rows),
        ]);
    }
}
