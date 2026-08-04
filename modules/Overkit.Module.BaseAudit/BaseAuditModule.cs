using Overkit.Contracts;
using Overkit.Sdk;

namespace Overkit.Module.BaseAudit;

/// <summary>
/// Audit de base (§6.1) — module de référence pour l'écosystème Overkit.
/// Surveille la faim et la santé mentale des travailleurs de chaque base et
/// produit une vue déclarative d'alertes. Aucune écriture, aucune UI propre :
/// juste un snapshot en entrée, une description de vue en sortie.
/// </summary>
public sealed class BaseAuditModule : IOverkitModule
{
    private IModuleContext _context = null!;
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;

    // Réglages pilotés par les sections interactives. Ils ne vivent que dans le
    // module : le host affiche les champs et renvoie la saisie, il ne conserve
    // rien.
    private double _warningThreshold = 50;
    private bool _criticalOnly;
    private string _baseFilter = AllBases;

    private const string AllBases = "*";

    public void OnInteraction(ViewInteraction interaction)
    {
        switch (interaction.Id)
        {
            case "threshold":
                _warningThreshold = Math.Clamp(interaction.AsNumber(), 5, 100);
                break;
            case "critical-only":
                _criticalOnly = interaction.AsBool();
                break;
            case "base":
                _baseFilter = interaction.Value;
                break;
        }
    }

    public ModuleManifest Manifest { get; } = new()
    {
        Id = "fr.overkit.base-audit",
        Name = "Audit de base",
        Version = "1.0.0",
        Authors = ["Nallraen"],
        License = "MIT",
        Homepage = "https://github.com/Overkit/overkit",
        StateRequires = ["bases"],
        StateOptional = ["palbox"],
        Capabilities = ["refdata"],
        MinSchema = "1.0",
    };

    public void Initialize(IModuleContext context) => _context = context;

    public void OnStateUpdated(GameStateSnapshot snapshot) => _snapshot = snapshot;

    public ModuleView BuildView()
    {
        var snapshot = _snapshot;

        if (snapshot.Bases?.List is not { Count: > 0 } bases)
        {
            return new ModuleView(Manifest.Name, [
                new EmptySection(snapshot.Mode == ConnectionMode.Static
                    ? "Données live indisponibles — la Sonde n'est pas connectée."
                    : "Aucune base détectée pour l'instant."),
            ]);
        }

        // Jointure instance_id -> Pal (le domaine palbox couvre aussi les
        // travailleurs) pour afficher un nom lisible plutôt qu'un GUID.
        var byInstance = new Dictionary<string, Pal>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Palbox?.Pals is { } pals)
        {
            foreach (var pal in pals)
            {
                byInstance[pal.Instance_id] = pal;
            }
        }

        var selected = bases
            .Where(b => _baseFilter == AllBases || b.Base_id == _baseFilter)
            .ToList();

        var alerts = new List<AlertItem>();
        var workers = 0;
        foreach (var baseInfo in selected)
        {
            if (baseInfo.Workers is null)
            {
                continue;
            }
            foreach (var worker in baseInfo.Workers)
            {
                workers++;
                var name = DisplayName(worker.Instance_id, byInstance);
                AddAlert(alerts, name, "faim", worker.Hunger);
                AddAlert(alerts, name, "santé mentale", worker.Sanity);
            }
        }

        if (_criticalOnly)
        {
            alerts.RemoveAll(a => a.Level != AlertLevel.Critical);
        }
        alerts.Sort((a, b) => b.Level.CompareTo(a.Level));

        var controls = BuildControls(bases);

        if (alerts.Count == 0)
        {
            return new ModuleView(Manifest.Name, [
                new StatusSection($"{workers} travailleurs surveillés sur {selected.Count} base(s)"),
                ..controls,
                new EmptySection(_criticalOnly
                    ? "Aucune alerte critique au seuil choisi. ✓"
                    : "Tout va bien : personne n'a faim ni ne déprime. ✓"),
            ]);
        }

        var critical = alerts.Count(a => a.Level == AlertLevel.Critical);
        return new ModuleView(Manifest.Name, [
            new StatusSection(critical > 0
                ? $"{alerts.Count} alertes dont {critical} critiques sur {workers} travailleurs"
                : $"{alerts.Count} alertes sur {workers} travailleurs"),
            ..controls,
            new AlertsSection(alerts),
        ]);
    }

    /// <summary>
    /// Champs de réglage. Ils décrivent l'état courant du module : le host les
    /// réaffiche tels quels et renvoie la saisie à OnInteraction.
    /// </summary>
    private List<ViewSection> BuildControls(IEnumerable<BaseInfo> bases)
    {
        var options = new List<ChoiceOption> { new(AllBases, "Toutes les bases") };
        options.AddRange(bases.Select((b, i) => new ChoiceOption(b.Base_id, $"Base {i + 1}")));

        return [
            new NumberInputSection("threshold", "Seuil d'alerte (%)", _warningThreshold, 5, 100, 5),
            new ChoiceSection("base", "Base surveillée", options, _baseFilter),
            new ToggleSection("critical-only", "Critiques seulement", _criticalOnly),
        ];
    }

    private void AddAlert(List<AlertItem> alerts, string palName, string gauge, Gauge? value)
    {
        if (value is null || value.Max <= 0)
        {
            return;
        }
        var percent = value.Current / value.Max * 100.0;
        if (percent >= _warningThreshold)
        {
            return;
        }
        alerts.Add(new AlertItem(
            percent < _warningThreshold / 2 ? AlertLevel.Critical : AlertLevel.Warning,
            palName,
            $"{gauge} à {percent:F0} % ({value.Current:F0}/{value.Max:F0})"));
    }

    private string DisplayName(string instanceId, Dictionary<string, Pal> byInstance)
    {
        if (!byInstance.TryGetValue(instanceId, out var pal))
        {
            return "Pal " + instanceId[..Math.Min(8, instanceId.Length)];
        }
        var species = _context.RefData?.SpeciesName(pal.Species_id) ?? pal.Species_id;
        return string.IsNullOrWhiteSpace(pal.Nickname) ? species : $"{pal.Nickname} ({species})";
    }
}
