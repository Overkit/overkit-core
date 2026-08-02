using Overkit.Contracts;

namespace Overkit.Host.Core;

public enum ConnectionMode
{
    /// <summary>Sonde connectée, données live.</summary>
    Live,

    /// <summary>Sans Sonde : mode statique de première classe (P3, EXG-010).</summary>
    Static,
}

/// <summary>
/// Snapshot immuable de l'état du jeu (§3.1). Reconstruit à chaque message de
/// la Sonde ; les consommateurs (HUD, panneau, modules à terme) reçoivent le
/// snapshot, jamais une référence vivante.
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
}
