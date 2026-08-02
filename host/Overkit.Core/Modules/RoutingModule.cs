using Overkit.Contracts;
using Overkit.Host.Core;

namespace Overkit.Host.Modules;

public sealed record FarmSpot(double X, double Y, double Z, int SpawnerCount, double DistanceMeters,
                              bool NightOnly, int LevelMin, int LevelMax);

/// <summary>
/// Module Routing de farm (§6.4, version minimale) : espèce → spots agrégés
/// (clustering des spawners), gate jour/nuit selon l'heure in-game, tri par
/// distance réelle au joueur.
/// </summary>
public static class RoutingModule
{
    private const double ClusterCellSize = 12_000; // cm — regroupe les spawners d'une même zone

    public static List<FarmSpot> FindSpots(string speciesId, GameStateSnapshot snapshot, RefData refData,
                                           bool respectTimeGate)
    {
        var spots = refData.SpawnSpotsFor(speciesId);
        if (spots.Count == 0)
        {
            return [];
        }

        // Nuit approximative Palworld : ~18 h à ~5 h. Heure inconnue = pas de gate.
        bool? isNight = snapshot.World?.Time is { Status: FieldStatus.Ok, Hour: { } hour }
            ? hour >= 18 || hour < 5
            : null;

        var (px, py) = snapshot.Player is { Status: FieldStatus.Ok, Position: { } p }
            ? (p.X, p.Y)
            : (double.NaN, double.NaN);

        // Clustering par cellule de grille : centroïde, niveaux et contrainte
        // horaire agrégés.
        var clusters = new Dictionary<(long, long), (double SumX, double SumY, double SumZ, int Count,
                                                     bool AllNight, int LvMin, int LvMax)>();
        foreach (var spot in spots)
        {
            if (respectTimeGate && isNight is { } night)
            {
                if (spot.OnlyTime == 2 && !night)
                {
                    continue; // spawner nocturne en pleine journée
                }
                if (spot.OnlyTime == 1 && night)
                {
                    continue;
                }
            }
            var key = ((long)Math.Floor(spot.X / ClusterCellSize), (long)Math.Floor(spot.Y / ClusterCellSize));
            var cluster = clusters.TryGetValue(key, out var existing)
                ? existing
                : (0, 0, 0, 0, true, int.MaxValue, 0);
            clusters[key] = (cluster.SumX + spot.X, cluster.SumY + spot.Y, cluster.SumZ + spot.Z,
                            cluster.Count + 1,
                            cluster.AllNight && spot.OnlyTime == 2,
                            Math.Min(cluster.LvMin, spot.LevelMin),
                            Math.Max(cluster.LvMax, spot.LevelMax));
        }

        var result = new List<FarmSpot>();
        foreach (var cluster in clusters.Values)
        {
            var x = cluster.SumX / cluster.Count;
            var y = cluster.SumY / cluster.Count;
            var distance = double.IsNaN(px)
                ? double.NaN
                : Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) / 100.0;
            result.Add(new FarmSpot(x, y, cluster.SumZ / cluster.Count, cluster.Count, distance,
                                    cluster.AllNight, cluster.LvMin, cluster.LvMax));
        }

        result.Sort((a, b) => double.IsNaN(a.DistanceMeters) ? 0 : a.DistanceMeters.CompareTo(b.DistanceMeters));
        return result;
    }
}

/// <summary>
/// Cible de farm courante, partagée entre le panneau (qui la définit) et le
/// HUD (qui affiche la distance en continu).
/// </summary>
public static class TargetService
{
    private static readonly object Lock = new();
    private static (string Name, double X, double Y, double Z)? _current;

    public static event Action? Changed;

    public static (string Name, double X, double Y, double Z)? Current
    {
        get
        {
            lock (Lock)
            {
                return _current;
            }
        }
    }

    public static void Set(string name, double x, double y, double z)
    {
        lock (Lock)
        {
            _current = (name, x, y, z);
        }
        Changed?.Invoke();
    }

    public static void Clear()
    {
        lock (Lock)
        {
            _current = null;
        }
        Changed?.Invoke();
    }
}
