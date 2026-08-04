using Overkit.Sdk;

namespace Overkit.Host.Cards;

/// <summary>
/// Exécute une Card : évalue ses expressions contre le snapshot et produit la
/// même <see cref="ModuleView"/> déclarative qu'un module C# — un seul moteur
/// de rendu pour les deux niveaux (ADR-0007).
///
/// Une Card qui dépasse son budget ou dont une expression est invalide est
/// suspendue avec un message explicite, jamais silencieusement (EXG-040).
/// </summary>
public sealed class CardRuntime(CardDefinition definition, string sourcePath)
{
    private GameStateSnapshot _snapshot = GameStateSnapshot.Empty;
    private DateTime _suspendedAt;
    private int _consecutiveFailures;

    /// <summary>Une suspension peut venir d'un pic passager : on retente, puis on abandonne.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private const int MaxConsecutiveFailures = 3;

    public CardDefinition Definition => definition;
    public string SourcePath => sourcePath;

    /// <summary>Card du joueur (modifiable et supprimable) plutôt que fournie avec Overkit.</summary>
    public bool IsUserCard { get; init; }
    public bool Suspended { get; private set; }
    public string? SuspendReason { get; private set; }

    public void OnStateUpdated(GameStateSnapshot snapshot) => _snapshot = snapshot;

    public ModuleView BuildView()
    {
        if (Suspended)
        {
            // Reprise automatique après un délai, sauf échecs répétés.
            if (_consecutiveFailures >= MaxConsecutiveFailures || DateTime.UtcNow - _suspendedAt < RetryDelay)
            {
                var suffix = _consecutiveFailures >= MaxConsecutiveFailures
                    ? " (abandon après plusieurs tentatives — corrige la Card puis relance Overkit)"
                    : " — nouvelle tentative dans quelques secondes";
                return new ModuleView(definition.Name, [new EmptySection($"Card suspendue : {SuspendReason}{suffix}")]);
            }
            Suspended = false;
        }

        var snapshot = _snapshot;

        var missing = definition.StateRequires
            .Where(domain => !snapshot.IsUsable(domain))
            .ToList();
        if (missing.Count > 0)
        {
            return new ModuleView(definition.Name, [
                new EmptySection(snapshot.Mode == ConnectionMode.Static
                    ? "Données live indisponibles — la Sonde n'est pas connectée."
                    : $"En attente des données : {string.Join(", ", missing)}"),
            ]);
        }

        var context = new ExpressionEngine.EvaluationContext();
        var sections = new List<ViewSection>();

        var index = 0;
        try
        {
            foreach (var section in definition.Sections)
            {
                index++;
                var built = BuildSection(section, snapshot, context);
                if (built is not null)
                {
                    sections.Add(built);
                }
            }
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            var reason = ex.GetBaseException().Message;
            var faulty = definition.Sections.ElementAtOrDefault(index - 1);
            Suspend($"section {index} ({faulty?.Type ?? "?"}) : {reason}");
            return new ModuleView(definition.Name, [new EmptySection($"Card suspendue : {SuspendReason}")]);
        }

        return sections.Count == 0
            ? new ModuleView(definition.Name, [new EmptySection("Rien à afficher pour l'instant.")])
            : new ModuleView(definition.Name, sections);
    }

    private void Suspend(string reason)
    {
        Suspended = true;
        SuspendReason = reason;
        _suspendedAt = DateTime.UtcNow;
        _consecutiveFailures++;
    }

    private ViewSection? BuildSection(CardSection section, GameStateSnapshot snapshot,
                                      ExpressionEngine.EvaluationContext context)
    {
        object? Eval(string? expression, object? scopeRoot = null) =>
            string.IsNullOrWhiteSpace(expression)
                ? null
                : ExpressionEngine.Evaluate(expression, scopeRoot ?? snapshot, context);

        // `when` au niveau section : masque tout le bloc.
        if (!section.ForEach && section.When is { Length: > 0 } condition &&
            !ExpressionEngine.Truthy(Eval(condition)))
        {
            return null;
        }

        switch (section.Type.ToLowerInvariant())
        {
            case "text":
                return new StatusSection(Interpolate(section.Text ?? "", snapshot, context));

            case "counters":
            {
                var items = section.Items
                    .Select(item => new CounterItem(item.Label, ExpressionEngine.AsString(Eval(item.Value))))
                    .ToList();
                return items.Count > 0 ? new CountersSection(items) : null;
            }

            case "gauges":
            {
                var items = new List<GaugeItem>();
                foreach (var item in section.Items)
                {
                    var current = ExpressionEngine.ToNumber(Eval(item.Current));
                    var max = ExpressionEngine.ToNumber(Eval(item.Max));
                    var emphasis = item.WarnBelow is { } threshold && max > 0 && current / max * 100 < threshold
                        ? AlertLevel.Warning
                        : (AlertLevel?)null;
                    items.Add(new GaugeItem(item.Label, current, max, emphasis));
                }
                return items.Count > 0 ? new GaugesSection(items) : null;
            }

            case "list":
            {
                var source = Eval(section.Source) as System.Collections.IEnumerable;
                var rows = new List<TableRow>();
                if (source is not null)
                {
                    var elements = source.Cast<object?>().ToList();
                    if (section.SortBy is { Length: > 0 } sortBy)
                    {
                        elements = section.SortDescending
                            ? elements.OrderByDescending(e => ExpressionEngine.ToNumber(Eval(sortBy, e))).ToList()
                            : elements.OrderBy(e => ExpressionEngine.ToNumber(Eval(sortBy, e))).ToList();
                    }
                    foreach (var element in elements.Take(Math.Clamp(section.Limit, 1, 500)))
                    {
                        context.CountScan();
                        rows.Add(new TableRow(
                            section.Columns.Select(c => ExpressionEngine.AsString(Eval(c.Value, element))).ToList()));
                    }
                }
                if (rows.Count == 0)
                {
                    return section.EmptyText is { Length: > 0 } empty ? new EmptySection(empty) : null;
                }
                return new TableSection(section.Columns.Select(c => c.Header).ToList(), rows);
            }

            case "alert":
            {
                var level = ParseLevel(section.Level);
                var alerts = new List<AlertItem>();

                if (section.ForEach)
                {
                    // Une alerte par élément retenu — le cas « alerte conditionnelle » du §5.1.
                    if (Eval(section.Source) is System.Collections.IEnumerable elements)
                    {
                        foreach (var element in elements.Cast<object?>().Take(200))
                        {
                            context.CountScan();
                            if (section.When is { Length: > 0 } itemCondition &&
                                !ExpressionEngine.Truthy(Eval(itemCondition, element)))
                            {
                                continue;
                            }
                            alerts.Add(new AlertItem(level,
                                Interpolate(section.Title ?? "", snapshot, context, element),
                                Interpolate(section.Detail ?? "", snapshot, context, element)));
                        }
                    }
                }
                else
                {
                    alerts.Add(new AlertItem(level,
                        Interpolate(section.Title ?? "", snapshot, context),
                        Interpolate(section.Detail ?? "", snapshot, context)));
                }

                return alerts.Count > 0 ? new AlertsSection(alerts) : null;
            }

            default:
                return new EmptySection($"Type de section inconnu : « {section.Type} »");
        }
    }

    /// <summary>Remplace les {expressions} d'un texte par leur valeur évaluée.</summary>
    private static string Interpolate(string template, GameStateSnapshot snapshot,
                                      ExpressionEngine.EvaluationContext context, object? scopeRoot = null)
    {
        if (!template.Contains('{'))
        {
            return template;
        }
        var result = new System.Text.StringBuilder(template.Length);
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{')
            {
                result.Append(template[i]);
                continue;
            }
            var end = template.IndexOf('}', i + 1);
            if (end < 0)
            {
                result.Append(template[i..]);
                break;
            }
            var expression = template[(i + 1)..end];
            result.Append(ExpressionEngine.AsString(
                ExpressionEngine.Evaluate(expression, scopeRoot ?? snapshot, context)));
            i = end;
        }
        return result.ToString();
    }

    private static AlertLevel ParseLevel(string level) => level.ToLowerInvariant() switch
    {
        "critical" or "critique" => AlertLevel.Critical,
        "info" => AlertLevel.Info,
        _ => AlertLevel.Warning,
    };
}
