using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Overkit.Contracts;
using Overkit.Host.Core;
using Overkit.Host.Modules;

namespace Overkit.Host.Views;

public sealed class SpotRow
{
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string SpeciesName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

/// <summary>
/// Vue carte (§9 Phase 2) : fond stylisé (l'image du jeu n'est pas
/// redistribuée, §10 — elle viendra de l'extraction locale par l'installeur),
/// marqueurs de bases, position live du joueur, spots de farm de l'espèce
/// recherchée (module Routing) et mise en cible pour le HUD.
/// </summary>
public sealed partial class MapView : UserControl
{
    private const double CanvasSize = 800;

    private StateBus _bus = null!;
    private RefData _refData = null!;
    private string? _speciesId;
    private List<FarmSpot> _spots = [];
    private Ellipse? _playerDot;
    private DateTime _lastPlayerDraw = DateTime.MinValue;

    private double _minX, _maxX, _minY, _maxY;

    public MapView()
    {
        InitializeComponent();

        SpeciesBox.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }
            var needle = box.Text.Trim();
            box.ItemsSource = needle.Length < 2
                ? null
                : _refData.AllSpecies
                    .Where(s => s.ZukanIndex > 0 && _refData.SpawnSpotsFor(s.Id).Count > 0 &&
                                (s.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
                                 s.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(12)
                    .Select(s => new SpeciesSuggestion(s.Id, s.Name))
                    .ToList();
        };
        SpeciesBox.SuggestionChosen += (box, args) =>
        {
            if (args.SelectedItem is SpeciesSuggestion suggestion)
            {
                box.Text = suggestion.Name;
                _speciesId = suggestion.Id;
                RefreshSpots();
            }
        };
        TimeGate.Toggled += (_, _) => RefreshSpots();
    }

    private sealed record SpeciesSuggestion(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public void Initialize(StateBus bus, RefData refData)
    {
        _bus = bus;
        _refData = refData;

        // Cadrage : bornes des spawners, avec une petite marge.
        var (minX, maxX, minY, maxY) = refData.SpawnBounds;
        var marginX = (maxX - minX) * 0.03;
        var marginY = (maxY - minY) * 0.03;
        _minX = minX - marginX;
        _maxX = maxX + marginX;
        _minY = minY - marginY;
        _maxY = maxY + marginY;

        var dispatcher = DispatcherQueue;
        _bus.SnapshotUpdated += snapshot =>
        {
            if ((DateTime.UtcNow - _lastPlayerDraw).TotalMilliseconds >= 500)
            {
                _lastPlayerDraw = DateTime.UtcNow;
                dispatcher.TryEnqueue(() => DrawPlayer(snapshot));
            }
        };

        StatusText.Text = _refData.HasSpawnData
            ? "Fond stylisé v1 — l'image de la carte arrivera avec l'installeur. Cherche une espèce pour voir ses spots."
            : "Dataset spawners absent.";
        DrawStaticLayers();
        DrawPlayer(_bus.Current);
    }

    // Monde (cm) -> canvas. Axes croisés comme la carte du jeu : l'axe
    // horizontal suit monde-Y, le vertical suit monde-X (nord en haut).
    private (double Cx, double Cy) ToCanvas(double worldX, double worldY)
    {
        var cx = (worldY - _minY) / (_maxY - _minY) * CanvasSize;
        var cy = CanvasSize - (worldX - _minX) / (_maxX - _minX) * CanvasSize;
        return (cx, cy);
    }

    private void DrawStaticLayers()
    {
        MapCanvas.Children.Clear();
        _playerDot = null;

        // Grille discrète.
        var gridBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(28, 255, 255, 255));
        for (var i = 1; i < 10; i++)
        {
            var offset = CanvasSize / 10 * i;
            MapCanvas.Children.Add(new Line { X1 = offset, Y1 = 0, X2 = offset, Y2 = CanvasSize, Stroke = gridBrush, StrokeThickness = 1 });
            MapCanvas.Children.Add(new Line { X1 = 0, Y1 = offset, X2 = CanvasSize, Y2 = offset, Stroke = gridBrush, StrokeThickness = 1 });
        }

        DrawSpots();
        DrawBases();
    }

    private void DrawBases()
    {
        if (_bus.Current.Bases?.List is not { } bases)
        {
            return;
        }
        foreach (var baseInfo in bases)
        {
            if (baseInfo.Position is not { } position)
            {
                continue;
            }
            var (cx, cy) = ToCanvas(position.X, position.Y);
            var marker = new TextBlock { Text = "⌂", FontSize = 20, Foreground = new SolidColorBrush(Colors.Gold) };
            Canvas.SetLeft(marker, cx - 8);
            Canvas.SetTop(marker, cy - 12);
            MapCanvas.Children.Add(marker);
        }
    }

    private void DrawSpots()
    {
        foreach (var spot in _spots)
        {
            var (cx, cy) = ToCanvas(spot.X, spot.Y);
            var size = Math.Clamp(6 + spot.SpawnerCount * 1.5, 6, 22);
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(spot.NightOnly ? Colors.MediumPurple : Colors.MediumSeaGreen),
                Opacity = 0.85,
            };
            Canvas.SetLeft(dot, cx - size / 2);
            Canvas.SetTop(dot, cy - size / 2);
            MapCanvas.Children.Add(dot);
        }
    }

    private void DrawPlayer(GameStateSnapshot snapshot)
    {
        if (snapshot.Player is not { Status: FieldStatus.Ok, Position: { } position })
        {
            return;
        }
        var (cx, cy) = ToCanvas(position.X, position.Y);
        if (_playerDot is null)
        {
            _playerDot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Colors.DeepSkyBlue),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
            };
            MapCanvas.Children.Add(_playerDot);
        }
        Canvas.SetLeft(_playerDot, cx - 6);
        Canvas.SetTop(_playerDot, cy - 6);
    }

    private void RefreshSpots()
    {
        SpotList.ItemsSource = null;
        _spots = [];
        if (_speciesId is null)
        {
            DrawStaticLayers();
            DrawPlayer(_bus.Current);
            return;
        }

        var snapshot = _bus.Current;
        _spots = RoutingModule.FindSpots(_speciesId, snapshot, _refData, TimeGate.IsOn);
        var name = _refData.SpeciesName(_speciesId);

        StatusText.Text = _spots.Count == 0
            ? $"Aucun spot connu pour {name}" + (TimeGate.IsOn ? " à cette heure (essaie sans le filtre jour/nuit)." : ".")
            : $"{name} : {_spots.Count} zones ({_spots.Sum(s => s.SpawnerCount)} spawners). " +
              "Violet = nocturne. 🎯 envoie la cible dans le HUD.";

        SpotList.ItemsSource = _spots.Take(15).Select(spot => new SpotRow
        {
            Title = double.IsNaN(spot.DistanceMeters)
                ? $"{spot.SpawnerCount} spawners"
                : $"{spot.DistanceMeters:N0} m — {spot.SpawnerCount} spawners",
            Detail = $"Nv {spot.LevelMin}-{spot.LevelMax}" + (spot.NightOnly ? " · nuit uniquement" : ""),
            SpeciesName = name,
            X = spot.X,
            Y = spot.Y,
            Z = spot.Z,
        }).ToList();

        DrawStaticLayers();
        DrawPlayer(snapshot);
    }

    private void Target_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SpotRow row)
        {
            TargetService.Set(row.SpeciesName, row.X, row.Y, row.Z);
        }
    }
}
