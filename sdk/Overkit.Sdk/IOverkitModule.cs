namespace Overkit.Sdk;

/// <summary>
/// Manifeste d'un module (§5.4) : ce qu'il est, ce dont il a besoin, ce qu'il
/// demande comme capacités. Le host résout la compatibilité au chargement et
/// désactive avec une raison affichée en cas d'échec (EXG-070).
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>Identifiant inverse-DNS, ex. « com.exemple.base-audit ».</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }
    public required string Version { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = [];
    public string License { get; init; } = "";
    public string Homepage { get; init; } = "";

    /// <summary>Domaines du State Bus sans lesquels le module ne peut pas fonctionner.</summary>
    public IReadOnlyList<string> StateRequires { get; init; } = [];

    /// <summary>Domaines exploités s'ils sont disponibles.</summary>
    public IReadOnlyList<string> StateOptional { get; init; } = [];

    /// <summary>Capacités demandées : « refdata », « storage ». Par défaut : aucune.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Version minimale du schéma State Bus supportée.</summary>
    public string MinSchema { get; init; } = "1.0";
}

/// <summary>Services fournis par le host à un module, selon les capacités déclarées.</summary>
public interface IModuleContext
{
    /// <summary>Données de référence — null si la capacité « refdata » n'est pas déclarée.</summary>
    IRefData? RefData { get; }

    /// <summary>Journalise un message côté host, préfixé par l'identifiant du module.</summary>
    void Log(string message);
}

/// <summary>
/// Contrat d'un module Overkit (§5.3). Un module observe l'état du jeu par
/// snapshots immuables et décrit une vue déclarative ; il n'écrit jamais vers
/// le jeu (P1) et ne crée aucune fenêtre (le host possède le layout).
///
/// Cycle de vie : Initialize une fois, puis OnStateUpdated à chaque snapshot,
/// et BuildView quand le host a besoin d'afficher. Toute exception levée
/// désactive le module et est signalée, sans affecter le host ni les autres
/// modules (EXG-060).
/// </summary>
public interface IOverkitModule
{
    ModuleManifest Manifest { get; }

    void Initialize(IModuleContext context);

    /// <summary>Appelé à chaque nouveau snapshot. Doit rester bref (pas d'IO, pas d'attente).</summary>
    void OnStateUpdated(GameStateSnapshot snapshot);

    /// <summary>Décrit ce qu'il faut afficher, à partir du dernier état connu.</summary>
    ModuleView BuildView();
}
