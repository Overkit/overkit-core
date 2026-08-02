using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Overkit.Contracts;
using Overkit.Host.Core;
using Windows.Graphics;

namespace Overkit.Host;

/// <summary>Ligne de la vue Palbox — projection UI d'un Pal du State Bus.</summary>
public sealed class PalRow
{
    public string DisplayName { get; set; } = "";
    public string SubName { get; set; } = "";
    public string GenderGlyph { get; set; } = "";
    public Brush GenderBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    public string LevelText { get; set; } = "";
    public string Talents { get; set; } = "";
    public string Passives { get; set; } = "";
}

/// <summary>
/// Panneau interactif (§2.2) : fenêtre WinUI 3 topmost ouverte par hotkey ou
/// depuis la zone de notification. Première vue : la Palbox en live — le
/// critère de sortie de la Phase 1.
/// </summary>
public sealed partial class PanelWindow : Window
{
    private readonly StateBus _bus;
    private object? _lastPalbox;

    public ObservableCollection<PalRow> Pals { get; } = [];

    public PanelWindow(StateBus bus)
    {
        _bus = bus;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1000, 680));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
        }

        // La croix cache le panneau (retour au jeu), elle ne tue pas le host.
        AppWindow.Closing += (sender, e) =>
        {
            e.Cancel = true;
            sender.Hide();
        };

        PalList.ItemsSource = Pals;

        var dispatcher = DispatcherQueue;
        _bus.SnapshotUpdated += snapshot =>
        {
            if (!ReferenceEquals(snapshot.Palbox, _lastPalbox))
            {
                dispatcher.TryEnqueue(() => Refresh(snapshot));
            }
        };
        Refresh(_bus.Current);
    }

    private void Refresh(GameStateSnapshot snapshot)
    {
        _lastPalbox = snapshot.Palbox;

        var palbox = snapshot.Palbox;
        if (palbox?.Pals is null || palbox.Status == FieldStatus.Unavailable)
        {
            StatusText.Text = snapshot.Mode == ConnectionMode.Static
                ? "données live indisponibles"
                : "en attente de la Sonde…";
            Pals.Clear();
            return;
        }

        StatusText.Text = palbox.Status == FieldStatus.Degraded && palbox.Owned_count is > 0
            ? $"{palbox.Pals.Count}/{palbox.Owned_count} synchronisés — ouvrir la boîte en jeu pour compléter"
            : $"{palbox.Pals.Count} Pals";

        Pals.Clear();
        foreach (var pal in palbox.Pals
                     .OrderByDescending(p => p.Level ?? 0)
                     .ThenBy(p => p.Species_id, StringComparer.OrdinalIgnoreCase))
        {
            var nickname = string.IsNullOrWhiteSpace(pal.Nickname) ? null : pal.Nickname;
            Pals.Add(new PalRow
            {
                DisplayName = nickname ?? pal.Species_id,
                SubName = nickname is null ? "" : pal.Species_id,
                GenderGlyph = pal.Gender switch
                {
                    PalGender.Male => "♂",
                    PalGender.Female => "♀",
                    _ => "•",
                },
                GenderBrush = new SolidColorBrush(pal.Gender switch
                {
                    PalGender.Male => Microsoft.UI.Colors.CornflowerBlue,
                    PalGender.Female => Microsoft.UI.Colors.LightPink,
                    _ => Microsoft.UI.Colors.Gray,
                }),
                LevelText = pal.Level is { } level ? $"Nv {level}" : "",
                Talents = pal.Talents is { } t
                    ? $"{t.Hp,3}/{t.Melee,3}/{t.Shot,3}/{t.Defense,3}"
                    : "",
                Passives = pal.Passives is { Count: > 0 } passives
                    ? string.Join(" · ", passives)
                    : "",
            });
        }
    }
}
