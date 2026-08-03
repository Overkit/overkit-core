namespace Overkit.Sdk;

/// <summary>
/// Vue déclarative produite par un module (§5.3) : le host possède le layout,
/// le module décrit seulement ce qu'il veut afficher. Aucune fenêtre, aucun
/// contrôle graphique n'est créé par le module — ce qui garantit qu'un module
/// ne peut ni casser l'UI du host, ni accéder à son modèle interne.
/// Les Cards (niveau 1) produisent le même modèle : un seul moteur de rendu.
/// </summary>
public sealed record ModuleView(string Title, IReadOnlyList<ViewSection> Sections)
{
    public static ModuleView Empty(string title) => new(title, []);
}

public abstract record ViewSection;

/// <summary>Ligne de contexte, en tête de vue (compteurs, état de synchro…).</summary>
public sealed record StatusSection(string Text) : ViewSection;

/// <summary>Message quand il n'y a rien à montrer (domaine indisponible, aucun résultat).</summary>
public sealed record EmptySection(string Message) : ViewSection;

/// <summary>Liste d'alertes triées par gravité.</summary>
public sealed record AlertsSection(IReadOnlyList<AlertItem> Items) : ViewSection;

public enum AlertLevel
{
    Info,
    Warning,
    Critical,
}

public sealed record AlertItem(AlertLevel Level, string Title, string Detail);

/// <summary>Tableau simple : en-têtes + lignes de cellules texte.</summary>
public sealed record TableSection(IReadOnlyList<string> Headers, IReadOnlyList<TableRow> Rows) : ViewSection;

public sealed record TableRow(IReadOnlyList<string> Cells, AlertLevel? Emphasis = null);

/// <summary>Jauges (faim, santé mentale, progression…) : valeur sur maximum.</summary>
public sealed record GaugesSection(IReadOnlyList<GaugeItem> Items) : ViewSection;

public sealed record GaugeItem(string Label, double Current, double Max, AlertLevel? Emphasis = null);

/// <summary>Compteurs mis en avant (« 52 Pals », « 6 448 or »).</summary>
public sealed record CountersSection(IReadOnlyList<CounterItem> Items) : ViewSection;

public sealed record CounterItem(string Label, string Value);
