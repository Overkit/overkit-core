using Overkit.Contracts;

namespace Overkit.Sdk;

public enum ConnectionMode
{
    /// <summary>Sonde connectée, données live.</summary>
    Live,

    /// <summary>Sans Sonde : mode statique de première classe (P3, EXG-010).</summary>
    Static,
}

/// <summary>
/// Snapshot immuable de l'état du jeu (§3.1) — la seule vue qu'un module a du
/// jeu. Reconstruit à chaque message de la Sonde ; un module reçoit le
/// snapshot, jamais une référence vivante vers le modèle du host (EXG-061).
/// Chaque domaine porte un statut : `unavailable` signifie « la Sonde n'a pas
/// pu lire ce domaine », pas « il est vide ».
/// </summary>
public sealed record GameStateSnapshot
{
    public ConnectionMode Mode { get; init; } = ConnectionMode.Static;
    public string? ProbeVersion { get; init; }
    public long ProbeTimeMs { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }

    public Player? Player { get; init; }
    public World? World { get; init; }
    public Inventory? Inventory { get; init; }
    public Palbox? Palbox { get; init; }
    public Party? Party { get; init; }
    public Bases? Bases { get; init; }
    public Nearby? Nearby { get; init; }
    public Collectors? Collectors { get; init; }

    public static readonly GameStateSnapshot Empty = new();

    /// <summary>Le domaine est-il exploitable (présent, et lu au moins partiellement) ?</summary>
    public bool IsUsable(string domain) => StatusOf(domain) is FieldStatus.Ok or FieldStatus.Degraded;

    /// <summary>Statut d'un domaine par son nom du State Bus (player, world, palbox…).</summary>
    public FieldStatus? StatusOf(string domain) => domain.ToLowerInvariant() switch
    {
        "player" => Player?.Status,
        "world" => World?.Status,
        "inventory" => Inventory?.Status,
        "palbox" => Palbox?.Status,
        "party" => Party?.Status,
        "bases" => Bases?.Status,
        "nearby" => Nearby?.Status,
        _ => null,
    };
}
