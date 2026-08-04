using Overkit.Sdk;

namespace Overkit.Host.Modules;

/// <summary>
/// Vue de la Checklist de craft (§6.2) : recette et quantité choisies, diff
/// avec l'inventaire, matériaux manquants et espèces qui les lâchent. Écrite en
/// déclaratif, comme un module tiers — le calcul vit dans
/// <see cref="CraftChecklistModule"/>, ce module ne fait que le présenter.
/// </summary>
public sealed class CraftModule : IOverkitModule
{
    private IModuleContext _context = null!;
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;

    private string _search = "";
    private string _recipeKey = "";
    private int _quantity = 1;

    /// <summary>Au-delà, la liste déroulante devient illisible : affiner la recherche.</summary>
    private const int MaxSuggestions = 12;

    public ModuleManifest Manifest { get; } = new()
    {
        Id = "fr.overkit.craft",
        Name = "Craft",
        Version = "1.0.0",
        Authors = ["Nallraen"],
        License = "MIT",
        Homepage = "https://github.com/Overkit/overkit",
        StateRequires = ["inventory"],
        StateOptional = [],
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

                // Changer la recherche invalide la recette choisie si elle
                // sort de la liste : garder une sélection invisible dérouterait.
                if (!Matches().Any(r => r.Key == _recipeKey))
                {
                    _recipeKey = "";
                }
                break;

            case "recipe":
                _recipeKey = interaction.Value;
                break;

            case "quantity":
                _quantity = Math.Clamp((int)interaction.AsNumber(), 1, 999);
                break;
        }
    }

    public ModuleView BuildView()
    {
        var refData = _context.RefData;
        if (refData is null)
        {
            return new ModuleView(Manifest.Name, [new EmptySection("Dataset indisponible.")]);
        }

        var matches = Matches().ToList();
        var sections = new List<ViewSection>
        {
            new TextInputSection("search", "", _search, "Rechercher une recette…"),
        };

        if (_search.Length < 2)
        {
            sections.Add(new EmptySection("Tape au moins deux lettres pour chercher une recette."));
            return new ModuleView(Manifest.Name, sections);
        }

        if (matches.Count == 0)
        {
            sections.Add(new EmptySection($"Aucune recette ne correspond à « {_search} »."));
            return new ModuleView(Manifest.Name, sections);
        }

        sections.Add(new ChoiceSection("recipe", "Recette",
            matches.Select(r => new ChoiceOption(r.Key, refData.ItemName(r.ProductId))).ToList(),
            _recipeKey));

        var recipe = matches.FirstOrDefault(r => r.Key == _recipeKey);
        if (recipe is null)
        {
            sections.Add(new EmptySection("Choisis une recette pour voir ce qu'il te manque."));
            return new ModuleView(Manifest.Name, sections);
        }

        sections.Add(new NumberInputSection("quantity", "Quantité", _quantity, 1, 999));

        var checklist = CraftChecklistModule.Compute(recipe, _quantity, _snapshot, refData);
        var product = refData.ItemName(recipe.ProductId);
        var missing = checklist.Lines.Count(l => l.Missing > 0);

        sections.Add(new StatusSection(checklist.Complete
            ? $"✓ Tout est en stock pour {_quantity} × {product}"
            : $"{missing} matériau(x) manquant(s) pour {_quantity} × {product}"));

        // L'inventaire non lu donnerait un « tout manque » trompeur.
        if (!_snapshot.IsUsable("inventory"))
        {
            sections.Add(new EmptySection(
                "Inventaire non lu par la Sonde — les quantités en stock affichées sont incomplètes."));
        }

        sections.Add(new TableSection(["", "MATÉRIAU", "STOCK", "OÙ EN TROUVER"],
            checklist.Lines.Select(line =>
            {
                var ok = line.Missing == 0;
                return new TableRow([
                    new TableCell(ok ? "✔" : "✖", ok ? null : AlertLevel.Critical),
                    line.ItemName,
                    new TableCell(ok ? $"{line.Have}/{line.Needed}" : $"{line.Have}/{line.Needed}  (−{line.Missing})",
                        ok ? null : AlertLevel.Critical),
                    line.Sources,
                ]);
            }).ToList()));

        return new ModuleView(Manifest.Name, sections);
    }

    private IEnumerable<RecipeInfo> Matches()
    {
        if (_context.RefData is not { } refData || _search.Length < 2)
        {
            return [];
        }
        return refData.Recipes
            .Where(r => refData.ItemName(r.ProductId).Contains(_search, StringComparison.CurrentCultureIgnoreCase) ||
                        r.ProductId.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions);
    }
}
