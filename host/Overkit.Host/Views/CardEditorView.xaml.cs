using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Overkit.Host.Cards;
using Overkit.Host.Core;
using Overkit.Sdk;

namespace Overkit.Host.Views;

public sealed record FilterChip(CardFilter Filter, string Label);

public sealed record BlockChip(CardSection Section, string Label);

public sealed class ColumnChoice(CardField field) : INotifyPropertyChanged
{
    private bool _selected;

    public CardField Field { get; } = field;
    public string Label => Field.Label;

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Éditeur de Cards in-game (§5.1) : le créateur choisit un type de bloc, une
/// source et des filtres dans des listes ; l'éditeur génère les expressions,
/// affiche un aperçu en direct sur les données réelles, et enregistre la Card
/// dans Cards/ — sans quitter le jeu ni écrire une ligne de JSON.
/// </summary>
public sealed partial class CardEditorView : UserControl
{
    private StateBus _bus = null!;
    private CardLoader _cards = null!;
    private string _cardsDirectory = "";

    private readonly ObservableCollection<FilterChip> _filters = [];
    private readonly ObservableCollection<BlockChip> _blocks = [];
    private readonly ObservableCollection<ColumnChoice> _columns = [];

    private CardRuntime? _preview;

    public CardEditorView()
    {
        InitializeComponent();

        FilterList.ItemsSource = _filters;
        BlockList.ItemsSource = _blocks;
        ColumnChecks.ItemsSource = _columns;

        SourceBox.ItemsSource = CardFieldCatalog.Sources;
        SourceBox.SelectedIndex = 0;
        GlobalBox.ItemsSource = CardFieldCatalog.Globals;
        GlobalBox.SelectedIndex = 0;
        FilterOperator.ItemsSource = CardFieldCatalog.Operators
            .Select(o => new { o.Symbol, o.Label })
            .ToList();
        FilterOperator.SelectedIndex = 0;

        BlockType.SelectionChanged += (_, _) => UpdateBlockTypeUi();
        UpdateBlockTypeUi();
    }

    public void Initialize(StateBus bus, CardLoader cards, string cardsDirectory)
    {
        _bus = bus;
        _cards = cards;
        _cardsDirectory = cardsDirectory;
        Preview.Initialize(bus, BuildPreview);
    }

    private string SelectedBlockType =>
        (BlockType.SelectedItem as ComboBoxItem)?.Tag as string ?? "counter";

    private CardSource CurrentSource =>
        SourceBox.SelectedItem as CardSource ?? CardFieldCatalog.Sources[0];

    private void UpdateBlockTypeUi()
    {
        var type = SelectedBlockType;
        var isGlobal = type == "global";

        SourceBox.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;
        FilterPanel.Visibility = isGlobal ? Visibility.Collapsed : Visibility.Visible;
        GlobalBox.Visibility = isGlobal ? Visibility.Visible : Visibility.Collapsed;
        ListOptions.Visibility = type == "list" ? Visibility.Visible : Visibility.Collapsed;
        AlertLevel.Visibility = type == "alert" ? Visibility.Visible : Visibility.Collapsed;
        BlockLabel.Visibility = type == "list" ? Visibility.Collapsed : Visibility.Visible;
        BlockLabel.Header = type switch
        {
            "alert" => "Titre de l'alerte",
            _ => "Intitulé affiché",
        };
    }

    private void Source_Changed(object sender, SelectionChangedEventArgs e)
    {
        var source = CurrentSource;
        FilterField.ItemsSource = source.Fields;
        FilterField.SelectedIndex = 0;
        SortField.ItemsSource = source.Fields.Where(f => f.Kind == CardFieldKind.Number).ToList();
        SortField.SelectedIndex = 0;

        _columns.Clear();
        foreach (var field in source.Fields)
        {
            var choice = new ColumnChoice(field) { Selected = _columns.Count < 3 };
            _columns.Add(choice);
        }
        _filters.Clear();
    }

    private void AddFilter_Click(object sender, RoutedEventArgs e)
    {
        if (FilterField.SelectedItem is not CardField field ||
            FilterOperator.SelectedItem is null ||
            string.IsNullOrWhiteSpace(FilterValue.Text))
        {
            SaveStatus.Text = "Choisis un champ, un opérateur et une valeur pour le filtre.";
            return;
        }
        var symbol = CardFieldCatalog.Operators[FilterOperator.SelectedIndex].Symbol;
        var filter = new CardFilter(field, symbol, FilterValue.Text.Trim());
        _filters.Add(new FilterChip(filter, filter.ToLabel()));
        FilterValue.Text = "";
        RefreshPreview();
    }

    private void RemoveFilter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FilterChip chip)
        {
            _filters.Remove(chip);
            RefreshPreview();
        }
    }

    private void AddBlock_Click(object sender, RoutedEventArgs e)
    {
        var type = SelectedBlockType;
        var filters = _filters.Select(f => f.Filter).ToList();
        var label = BlockLabel.Text.Trim();

        CardSection section;
        string chipLabel;

        switch (type)
        {
            case "global":
            {
                if (GlobalBox.SelectedItem is not CardField field)
                {
                    return;
                }
                var text = label.Length > 0 ? label : field.Label;
                section = CardBuilder.BuildGlobalCounter(text, field);
                chipLabel = $"Valeur — {text}";
                break;
            }

            case "list":
            {
                var columns = _columns.Where(c => c.Selected).Select(c => c.Field).ToList();
                if (columns.Count == 0)
                {
                    SaveStatus.Text = "Coche au moins une colonne à afficher.";
                    return;
                }
                section = CardBuilder.BuildList(CurrentSource, filters, columns,
                                                SortField.SelectedItem as CardField, SortDesc.IsOn, 25);
                chipLabel = $"Liste — {CurrentSource.Label}" +
                            (filters.Count > 0 ? $" ({filters.Count} filtre(s))" : "");
                break;
            }

            case "alert":
            {
                if (label.Length == 0)
                {
                    SaveStatus.Text = "Donne un titre à ton alerte.";
                    return;
                }
                var level = (AlertLevel.SelectedItem as ComboBoxItem)?.Tag as string ?? "warning";
                section = CardBuilder.BuildAlert(label, CurrentSource, filters, level);
                chipLabel = $"Alerte — {label}";
                break;
            }

            default:
            {
                var text = label.Length > 0 ? label : CurrentSource.Label;
                section = CardBuilder.BuildCounter(text, CurrentSource, filters);
                chipLabel = $"Compteur — {text}";
                break;
            }
        }

        _blocks.Add(new BlockChip(section, chipLabel));
        _filters.Clear();
        BlockLabel.Text = "";
        NoBlocks.Visibility = Visibility.Collapsed;
        SaveStatus.Text = "";
        RefreshPreview();
    }

    private void RemoveBlock_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BlockChip chip)
        {
            _blocks.Remove(chip);
            NoBlocks.Visibility = _blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshPreview();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = CardName.Text.Trim();
        if (name.Length == 0)
        {
            SaveStatus.Text = "Donne un nom à ta card.";
            return;
        }
        if (_blocks.Count == 0)
        {
            SaveStatus.Text = "Ajoute au moins un bloc avant d'enregistrer.";
            return;
        }

        try
        {
            var card = CardBuilder.BuildCard(name, _blocks.Select(b => b.Section), Environment.UserName);
            _cards.SaveAndLoad(card, _cardsDirectory);
            SaveStatus.Text = $"« {name} » enregistrée — son onglet est disponible tout de suite.";
        }
        catch (Exception ex)
        {
            SaveStatus.Text = $"Enregistrement impossible : {ex.GetBaseException().Message}";
        }
    }

    /// <summary>Aperçu : une Card temporaire construite à partir des blocs en cours.</summary>
    private ModuleView BuildPreview()
    {
        if (_preview is null)
        {
            return new ModuleView("Aperçu", [new EmptySection("Ajoute un bloc pour voir le résultat ici.")]);
        }
        _preview.OnStateUpdated(_bus.Current);
        return _preview.BuildView();
    }

    private void RefreshPreview()
    {
        if (_blocks.Count == 0)
        {
            _preview = null;
            return;
        }
        var name = CardName.Text.Trim();
        var definition = CardBuilder.BuildCard(name.Length > 0 ? name : "Aperçu",
                                               _blocks.Select(b => b.Section), Environment.UserName);
        _preview = new CardRuntime(definition, "");
        _preview.OnStateUpdated(_bus.Current);
    }
}
