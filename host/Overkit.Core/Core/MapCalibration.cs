using System.Text.Json;

namespace Overkit.Host.Core;

/// <summary>
/// Transformation affine monde → carte in-game, chargée depuis le dataset
/// (P6 : rien en dur). Absente = coordonnées carte simplement indisponibles.
/// </summary>
public sealed class MapCalibration
{
    private readonly double _xScale, _xOffset, _yScale, _yOffset;
    private readonly bool _xFromWorldY, _yFromWorldX;

    private MapCalibration(double xScale, double xOffset, bool xFromWorldY,
                           double yScale, double yOffset, bool yFromWorldX)
    {
        _xScale = xScale;
        _xOffset = xOffset;
        _xFromWorldY = xFromWorldY;
        _yScale = yScale;
        _yOffset = yOffset;
        _yFromWorldX = yFromWorldX;
    }

    public (double MapX, double MapY) WorldToMap(double worldX, double worldY)
    {
        var mapX = (_xFromWorldY ? worldY : worldX) * _xScale + _xOffset;
        var mapY = (_yFromWorldX ? worldX : worldY) * _yScale + _yOffset;
        return (mapX, mapY);
    }

    /// <summary>
    /// Cherche map_calibration(.draft).json dans le dossier data à côté de
    /// l'exécutable, puis en remontant vers la racine du repo (confort dev).
    /// </summary>
    public static MapCalibration? TryLoad(Action<string> log)
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var transform = doc.RootElement.GetProperty("world_to_map");
                var mapX = transform.GetProperty("map_x");
                var mapY = transform.GetProperty("map_y");
                var calibration = new MapCalibration(
                    mapX.GetProperty("scale").GetDouble(),
                    mapX.GetProperty("offset").GetDouble(),
                    mapX.GetProperty("source_axis").GetString() == "world_y",
                    mapY.GetProperty("scale").GetDouble(),
                    mapY.GetProperty("offset").GetDouble(),
                    mapY.GetProperty("source_axis").GetString() == "world_x");
                log($"Calibration carte chargée : {path}");
                return calibration;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
            {
                log($"Calibration carte illisible ({path}) : {ex.Message}");
            }
        }
        log("Calibration carte absente : coordonnées carte indisponibles");
        return null;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "data", "map_calibration.json");

        // Confort dev : remonter jusqu'à la racine du repo pour trouver dataset/.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "dataset", "map_calibration.draft.json");
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }
}
