using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Overkit.Host.Cards;
using Overkit.Host.Core;
using Overkit.Host.Views;
using Overkit.Sdk;
using Windows.Graphics;

namespace Overkit.Host;

/// <summary>
/// Panneau interactif (§2.2) : fenêtre WinUI 3 topmost. Les vues intégrées
/// (Palbox, Craft, Accouplement, Carte) et les modules chargés dynamiquement
/// (§5.3) partagent la même navigation — un module tiers apparaît comme un
/// onglet de plein droit.
/// </summary>
public sealed partial class PanelWindow : Window
{
    private readonly Dictionary<string, UIElement> _pages = new(StringComparer.Ordinal);

    private readonly StateBus _bus;

    public PanelWindow(StateBus bus, RefData refData, ModuleLoader loader, CardLoader cards)
    {
        _bus = bus;
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

        Palbox.Initialize(bus, refData);
        Craft.Initialize(bus, refData);
        Breeding.Initialize(bus, refData);
        Map.Initialize(bus, refData);

        Editor.Initialize(bus, cards);

        _pages["palbox"] = Palbox;
        _pages["craft"] = Craft;
        _pages["breeding"] = Breeding;
        _pages["map"] = Map;
        _pages["editor"] = Editor;

        AddModuleTabs(bus, loader);
        AddCardTabs(bus, cards);

        // L'éditeur agit à chaud : création, mise à jour et suppression se
        // répercutent immédiatement sur les onglets.
        cards.CardSaved += (card, replaced) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                if (replaced)
                {
                    UpdateCardTab(card);
                }
                else
                {
                    AddCardTab(card);
                }
            });

        cards.CardDeleted += card =>
            DispatcherQueue.TryEnqueue(() => RemoveCardTab(card));
    }

    /// <summary>Un onglet par Card chargée — même rendu que les modules.</summary>
    private void AddCardTabs(StateBus bus, CardLoader cards)
    {
        foreach (var card in cards.Cards)
        {
            AddCardTab(card);
        }
    }

    private void AddCardTab(CardRuntime card)
    {
        var view = new ModuleHostView { Visibility = Visibility.Collapsed };
        view.Initialize(_bus, card.BuildView);

        var tag = "card:" + card.Definition.Id;
        _pages[tag] = view;
        Pages.Children.Add(view);

        // Les cards se placent avant l'éditeur, qui reste le dernier onglet.
        var editorIndex = Nav.MenuItems
            .OfType<NavigationViewItem>()
            .ToList()
            .FindIndex(item => (item.Tag as string) == "editor");
        var newItem = new NavigationViewItem { Content = card.Definition.Name, Tag = tag };
        if (editorIndex >= 0)
        {
            Nav.MenuItems.Insert(editorIndex, newItem);
        }
        else
        {
            Nav.MenuItems.Add(newItem);
        }
    }

    /// <summary>Un onglet par module chargé, inséré après la Palbox.</summary>
    private void AddModuleTabs(StateBus bus, ModuleLoader loader)
    {
        var insertAt = 1;
        foreach (var module in loader.Modules)
        {
            var view = new ModuleHostView();
            view.Visibility = Visibility.Collapsed;
            view.Initialize(bus, loader, module);

            var tag = "module:" + module.Manifest.Id;
            _pages[tag] = view;
            Pages.Children.Add(view);

            var item = new NavigationViewItem
            {
                Content = module.Manifest.Name,
                Tag = tag,
            };
            if (module.Status != ModuleStatus.Active)
            {
                item.Content = module.Manifest.Name + " ⚠";
                ToolTipService.SetToolTip(item, module.Reason);
            }
            Nav.MenuItems.Insert(Math.Min(insertAt++, Nav.MenuItems.Count), item);
        }
    }

    /// <summary>Card modifiée : la vue est reconstruite et l'onglet renommé si besoin.</summary>
    private void UpdateCardTab(CardRuntime card)
    {
        var tag = "card:" + card.Definition.Id;
        if (_pages.TryGetValue(tag, out var page) && page is ModuleHostView view)
        {
            view.Initialize(_bus, card.BuildView);
        }
        var item = Nav.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(i => (i.Tag as string) == tag);
        if (item is not null)
        {
            item.Content = card.Definition.Name;
        }
    }

    private void RemoveCardTab(CardRuntime card)
    {
        var tag = "card:" + card.Definition.Id;
        if (_pages.Remove(tag, out var page))
        {
            Pages.Children.Remove(page);
        }
        var item = Nav.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(i => (i.Tag as string) == tag);
        if (item is not null)
        {
            var wasSelected = ReferenceEquals(Nav.SelectedItem, item);
            Nav.MenuItems.Remove(item);
            if (wasSelected)
            {
                Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            }
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if ((args.SelectedItem as NavigationViewItem)?.Tag is not string tag)
        {
            return;
        }
        foreach (var (key, page) in _pages)
        {
            page.Visibility = key == tag ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
