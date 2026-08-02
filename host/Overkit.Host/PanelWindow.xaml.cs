using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
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
    public string IvHp { get; set; } = "";
    public string IvMelee { get; set; } = "";
    public string IvShot { get; set; } = "";
    public string IvDefense { get; set; } = "";
    public string Passives { get; set; } = "";

    public int LevelValue { get; set; }
    public int IvTotal { get; set; }
    public string SearchText { get; set; } = "";
}

/// <summary>
/// Panneau interactif (§2.2) : fenêtre WinUI 3 topmost ouverte par hotkey ou
/// depuis la zone de notification. Première vue : la Palbox en live avec
/// recherche et tri — le critère de sortie de la Phase 1.
/// </summary>
public sealed partial class PanelWindow : Window
{
    private readonly StateBus _bus;
    private readonly RefData _refData;
    private object? _lastPalbox;
    private List<PalRow> _all = [];

    public ObservableCollection<PalRow> Pals { get; } = [];

    public PanelWindow(StateBus bus, RefData refData)
    {
        _bus = bus;
        _refData = refData;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1080, 720));
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
        SearchBox.TextChanged += (_, _) => ApplyView();
        SortBox.SelectionChanged += (_, _) => ApplyView();

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
            _all = [];
            ApplyView();
            return;
        }

        StatusText.Text = palbox.Status == FieldStatus.Degraded && palbox.Owned_count is > 0
            ? $"{palbox.Pals.Count}/{palbox.Owned_count} synchronisés — ouvrir la boîte en jeu pour compléter"
            : $"{palbox.Pals.Count} Pals";

        var partyIds = snapshot.Party?.Member_instance_ids is { Count: > 0 } ids
            ? new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase)
            : [];
        _all = palbox.Pals.Select(pal => ToRow(pal, partyIds.Contains(pal.Instance_id))).ToList();
        ApplyView();
    }

    private void ApplyView()
    {
        IEnumerable<PalRow> view = _all;

        var query = SearchBox.Text?.Trim() ?? "";
        if (query.Length > 0)
        {
            var needle = query.ToLowerInvariant();
            view = view.Where(r => r.SearchText.Contains(needle, StringComparison.Ordinal));
        }

        view = SortBox.SelectedIndex switch
        {
            1 => view.OrderBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            2 => view.OrderByDescending(r => r.IvTotal).ThenByDescending(r => r.LevelValue),
            _ => view.OrderByDescending(r => r.LevelValue)
                     .ThenBy(r => r.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };

        Pals.Clear();
        foreach (var row in view)
        {
            Pals.Add(row);
        }
    }

    private PalRow ToRow(Pal pal, bool inParty)
    {
        var species = _refData.SpeciesName(pal.Species_id);
        var nickname = string.IsNullOrWhiteSpace(pal.Nickname) ? null : pal.Nickname;
        var passives = pal.Passives is { Count: > 0 } list
            ? string.Join("  ·  ", list.Select(_refData.PassiveName))
            : "—";
        var talents = pal.Talents;
        var ivTotal = (talents?.Hp ?? 0) + (talents?.Melee ?? 0) + (talents?.Shot ?? 0) + (talents?.Defense ?? 0);

        return new PalRow
        {
            DisplayName = (inParty ? "★ " : "") + (nickname ?? species),
            SubName = nickname is null ? (inParty ? "dans l'équipe" : "") : species,
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
            LevelText = pal.Level is { } level ? $"Nv {level}" : "—",
            LevelValue = pal.Level ?? 0,
            IvHp = talents?.Hp?.ToString() ?? "?",
            IvMelee = talents?.Melee?.ToString() ?? "?",
            IvShot = talents?.Shot?.ToString() ?? "?",
            IvDefense = talents?.Defense?.ToString() ?? "?",
            IvTotal = ivTotal,
            Passives = passives,
            SearchText = $"{nickname} {species} {pal.Species_id} {passives}".ToLowerInvariant(),
        };
    }

    /// <summary>
    /// Aère un identifiant interne (« SamuraiDog » → « Samurai Dog »). Les
    /// vrais noms localisés arriveront avec le dataset (refdata, P6).
    /// </summary>
    private static string PrettifySpecies(string id)
    {
        var cleaned = id.Replace('_', ' ').Trim();
        return Regex.Replace(cleaned, "(?<=[a-zà-ÿ0-9])(?=[A-Z])", " ");
    }
}
