using Overkit.Sdk;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Overkit.Host.Core;
using Overkit.Host.Modules;

namespace Overkit.Host.Views;

public sealed class PairRow
{
    public string Glyph { get; set; } = "";
    public string PairText { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>
/// Vue du module Accouplement inversé : espèce cible → paires de parents,
/// celles réalisables avec la Palbox (genres inclus) en tête.
/// </summary>
public sealed partial class BreedingView : UserControl
{
    private StateBus _bus = null!;
    private RefData _refData = null!;
    private string? _targetId;

    public ObservableCollection<PairRow> Pairs { get; } = [];

    public BreedingView()
    {
        InitializeComponent();
        PairList.ItemsSource = Pairs;

        TargetBox.TextChanged += (box, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }
            var needle = box.Text.Trim();
            box.ItemsSource = needle.Length < 2
                ? null
                : _refData.AllSpecies
                    .Where(s => s.ZukanIndex > 0 &&
                                (s.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase) ||
                                 s.Id.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Take(12)
                    .Select(s => new SpeciesSuggestion(s.Id, s.Name))
                    .ToList();
        };
        TargetBox.SuggestionChosen += (box, args) =>
        {
            if (args.SelectedItem is SpeciesSuggestion suggestion)
            {
                box.Text = suggestion.Name;
                _targetId = suggestion.Id;
                Refresh();
            }
        };
        OwnedOnly.Toggled += (_, _) => Refresh();
    }

    private sealed record SpeciesSuggestion(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public void Initialize(StateBus bus, RefData refData)
    {
        _bus = bus;
        _refData = refData;
        StatusText.Text = "Choisis l'espèce que tu veux obtenir : je liste les parents possibles, " +
                          "en commençant par les paires réalisables avec ta Palbox.";
    }

    private void Refresh()
    {
        Pairs.Clear();
        if (_targetId is null)
        {
            return;
        }

        var result = BreedingModule.FindPairs(_targetId, _bus.Current, _refData);
        var view = OwnedOnly.IsOn ? result.Pairs.Where(p => p.Owned).ToList() : result.Pairs.ToList();
        var ownedCount = result.Pairs.Count(p => p.Owned);

        StatusText.Text = result.TotalPairs == 0
            ? $"Aucune paire connue ne produit {result.TargetName}."
            : $"{result.TargetName} : {result.TotalPairs} paire(s) possible(s), dont {ownedCount} réalisable(s) avec ta Palbox" +
              (result.TotalPairs > result.Pairs.Count ? $" (affichage limité à {result.Pairs.Count})" : "") +
              " — probabilités de passifs non calculées : des possibilités, pas des certitudes.";

        foreach (var pair in view)
        {
            Pairs.Add(new PairRow
            {
                Glyph = pair.Owned ? "✔" : "•",
                PairText = $"{pair.ParentAName}  ×  {pair.ParentBName}",
                Note = pair.Note,
            });
        }
    }
}
