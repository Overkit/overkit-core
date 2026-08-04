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

/// <summary>
/// Tableau simple : en-têtes + lignes de cellules texte. Renseigner
/// <paramref name="SelectionId"/> rend les lignes cliquables : le clic envoie
/// au module une interaction portant cet identifiant et la clé de la ligne.
/// </summary>
public sealed record TableSection(
    IReadOnlyList<string> Headers,
    IReadOnlyList<TableRow> Rows,
    string? SelectionId = null) : ViewSection;

/// <param name="Key">Clé renvoyée au clic — requise pour une ligne sélectionnable.</param>
public sealed record TableRow(IReadOnlyList<TableCell> Cells, AlertLevel? Emphasis = null, string? Key = null, bool Selected = false);

/// <summary>
/// Cellule d'un tableau. La conversion implicite depuis une chaîne garde le
/// cas courant lisible — <c>new TableRow(["Lamball", "12"])</c> — tout en
/// laissant colorer une cellule seule ou lui adjoindre une ligne secondaire
/// (espèce sous le surnom, quantité sous un total).
/// </summary>
public sealed record TableCell(string Text, AlertLevel? Emphasis = null, string? Secondary = null)
{
    public static implicit operator TableCell(string text) => new(text);
}

/// <summary>Jauges (faim, santé mentale, progression…) : valeur sur maximum.</summary>
public sealed record GaugesSection(IReadOnlyList<GaugeItem> Items) : ViewSection;

public sealed record GaugeItem(string Label, double Current, double Max, AlertLevel? Emphasis = null);

/// <summary>Compteurs mis en avant (« 52 Pals », « 6 448 or »).</summary>
public sealed record CountersSection(IReadOnlyList<CounterItem> Items) : ViewSection;

public sealed record CounterItem(string Label, string Value);

// --- Sections interactives -------------------------------------------------
//
// Un module ne crée aucun contrôle : il déclare un champ, le host l'affiche et
// lui renvoie la saisie via OnInteraction. L'interaction ne porte que sur la
// vue du module (filtrer, sélectionner, recalculer) — le jeu reste en lecture
// seule (P1). L'identifiant d'une section doit être stable d'un rendu à
// l'autre, c'est lui qui relie le contrôle affiché à la valeur reçue.

/// <summary>Champ de recherche ou de saisie libre.</summary>
/// <param name="Value">Valeur courante, telle que le module la connaît — le host la réaffiche.</param>
public sealed record TextInputSection(string Id, string Label, string Value = "", string Placeholder = "") : ViewSection;

/// <summary>Saisie numérique bornée (quantité à crafter, rayon de recherche…).</summary>
public sealed record NumberInputSection(
    string Id,
    string Label,
    double Value,
    double Min = 0,
    double Max = 9999,
    double Step = 1) : ViewSection;

/// <summary>Choix unique parmi une liste fermée (tri, filtre par élément…).</summary>
public sealed record ChoiceSection(
    string Id,
    string Label,
    IReadOnlyList<ChoiceOption> Options,
    string? SelectedValue = null) : ViewSection;

public sealed record ChoiceOption(string Value, string Label);

/// <summary>Interrupteur — l'interaction renvoie « true » ou « false ».</summary>
public sealed record ToggleSection(string Id, string Label, bool Value = false) : ViewSection;

/// <summary>Boutons d'action. Le clic renvoie l'identifiant du bouton, sans valeur.</summary>
public sealed record ActionsSection(IReadOnlyList<ActionItem> Items) : ViewSection;

public sealed record ActionItem(string Id, string Label, bool IsPrimary = false);
