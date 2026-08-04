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
    private const double WarningThreshold = 50;
    private const double CriticalThreshold = 25;

    private IModuleContext _context = null!;
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;

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

        var alerts = new List<AlertItem>();
        var workers = 0;
        foreach (var baseInfo in bases)
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

        alerts.Sort((a, b) => b.Level.CompareTo(a.Level));

        if (alerts.Count == 0)
        {
            return new ModuleView(Manifest.Name, [
                new StatusSection($"{workers} travailleurs surveillés sur {bases.Count} base(s)"),
                new EmptySection("Tout va bien : personne n'a faim ni ne déprime. ✓"),
            ]);
        }

        var critical = alerts.Count(a => a.Level == AlertLevel.Critical);
        return new ModuleView(Manifest.Name, [
            new StatusSection(critical > 0
                ? $"{alerts.Count} alertes dont {critical} critiques sur {workers} travailleurs"
                : $"{alerts.Count} alertes sur {workers} travailleurs"),
            new AlertsSection(alerts),
        ]);
    }

    private static void AddAlert(List<AlertItem> alerts, string palName, string gauge, Gauge? value)
    {
        if (value is null || value.Max <= 0)
        {
            return;
        }
        var percent = value.Current / value.Max * 100.0;
        if (percent >= WarningThreshold)
        {
            return;
        }
        alerts.Add(new AlertItem(
            percent < CriticalThreshold ? AlertLevel.Critical : AlertLevel.Warning,
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
