using Overkit.Sdk;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Overkit.Host.Core;
using Overkit.Host.Modules;

namespace Overkit.Host.Views;

public sealed class CraftRow
{
    public string Glyph { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string CountText { get; set; } = "";
    public Brush CountBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gainsboro);
    public string Sources { get; set; } = "";
}

/// <summary>Vue du module Checklist de craft : recette → il manque quoi, où le trouver.</summary>
public sealed partial class CraftView : UserControl
{
    private StateBus _bus = null!;
    private RefData _refData = null!;
    private RecipeInfo? _selected;
    private object? _lastInventory;

    public ObservableCollection<CraftRow> Lines { get; } = [];

    public CraftView()
    {
        InitializeComponent();
        LineList.ItemsSource = Lines;

        RecipeBox.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }
            var needle = box.Text.Trim();
            box.ItemsSource = needle.Length < 2
                ? null
                : _refData.Recipes
                    .Where(r => _refData.ItemName(r.ProductId).Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
                                r.ProductId.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    .Take(12)
                    .Select(r => new RecipeSuggestion(r, _refData.ItemName(r.ProductId)))
                    .ToList();
        };
        RecipeBox.SuggestionChosen += (box, args) =>
        {
            if (args.SelectedItem is RecipeSuggestion suggestion)
            {
                box.Text = suggestion.DisplayName;
                _selected = suggestion.Recipe;
                Refresh();
            }
        };
        QuantityBox.ValueChanged += (_, _) => Refresh();
    }

    private sealed record RecipeSuggestion(RecipeInfo Recipe, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public void Initialize(StateBus bus, RefData refData)
    {
        _bus = bus;
        _refData = refData;
        var dispatcher = DispatcherQueue;
        _bus.SnapshotUpdated += snapshot =>
        {
            if (_selected is not null && !ReferenceEquals(snapshot.Inventory, _lastInventory))
            {
                dispatcher.TryEnqueue(Refresh);
            }
        };
        StatusText.Text = "Choisis une recette pour voir ce qu'il te manque.";
    }

    private void Refresh()
    {
        var snapshot = _bus.Current;
        _lastInventory = snapshot.Inventory;
        Lines.Clear();
        if (_selected is null)
        {
            return;
        }

        var quantity = double.IsNaN(QuantityBox.Value) ? 1 : Math.Max(1, (int)QuantityBox.Value);
        var checklist = CraftChecklistModule.Compute(_selected, quantity, snapshot, _refData);

        StatusText.Text = checklist.Complete
            ? $"✓ Tout est en stock pour {quantity} × {_refData.ItemName(_selected.ProductId)}"
            : $"{checklist.Lines.Count(l => l.Missing > 0)} matériau(x) manquant(s) pour {quantity} × {_refData.ItemName(_selected.ProductId)}";

        foreach (var line in checklist.Lines)
        {
            var ok = line.Missing == 0;
            Lines.Add(new CraftRow
            {
                Glyph = ok ? "✔" : "✖",
                ItemName = line.ItemName,
                CountText = ok ? $"{line.Have}/{line.Needed}" : $"{line.Have}/{line.Needed}  (−{line.Missing})",
                CountBrush = new SolidColorBrush(ok ? Microsoft.UI.Colors.LightGreen : Microsoft.UI.Colors.OrangeRed),
                Sources = line.Sources,
            });
        }
    }
}
