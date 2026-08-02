using Overkit.Contracts;

namespace Overkit.Host.Core;

/// <summary>
/// Le State Bus (§3) : maintient le snapshot courant et le remplace de façon
/// atomique. Un domaine absent d'un message est réputé inchangé ; un domaine
/// présent remplace intégralement sa version précédente.
/// </summary>
public sealed class StateBus
{
    private readonly object _lock = new();
    private GameStateSnapshot _current = GameStateSnapshot.Empty;

    /// <summary>Déclenché après chaque remplacement de snapshot (thread quelconque).</summary>
    public event Action<GameStateSnapshot>? SnapshotUpdated;

    public GameStateSnapshot Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Apply(StateMessage message)
    {
        GameStateSnapshot next;
        lock (_lock)
        {
            next = _current with
            {
                Mode = ConnectionMode.Live,
                ProbeTimeMs = (long)message.T_ms,
                ReceivedAt = DateTimeOffset.UtcNow,
                Player = message.Player ?? _current.Player,
                World = message.World ?? _current.World,
                Inventory = message.Inventory ?? _current.Inventory,
                Palbox = message.Palbox ?? _current.Palbox,
                Party = message.Party ?? _current.Party,
                Bases = message.Bases ?? _current.Bases,
                Nearby = message.Nearby ?? _current.Nearby,
                Collectors = message.Collectors ?? _current.Collectors,
            };
            _current = next;
        }
        SnapshotUpdated?.Invoke(next);
    }

    public void SetHandshake(HandshakeMessage handshake)
    {
        GameStateSnapshot next;
        lock (_lock)
        {
            next = _current with { ProbeVersion = handshake.Probe_version };
            _current = next;
        }
        SnapshotUpdated?.Invoke(next);
    }

    /// <summary>Bascule en mode statique : les données de référence restent, le live est marqué périmé (P3).</summary>
    public void EnterStaticMode()
    {
        GameStateSnapshot next;
        lock (_lock)
        {
            if (_current.Mode == ConnectionMode.Static)
            {
                return;
            }
            next = _current with { Mode = ConnectionMode.Static };
            _current = next;
        }
        SnapshotUpdated?.Invoke(next);
    }
}
