using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Overkit.Host.Core;
using Windows.Graphics;

namespace Overkit.Host;

/// <summary>
/// Panneau interactif (§2.2) : fenêtre WinUI 3 topmost, navigation entre la
/// Palbox et les trois modules fondateurs de la Phase 2.
/// </summary>
public sealed partial class PanelWindow : Window
{
    public PanelWindow(StateBus bus, RefData refData)
    {
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
        Audit.Initialize(bus, refData);
        Craft.Initialize(bus, refData);
        Breeding.Initialize(bus, refData);
        Map.Initialize(bus, refData);
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        Palbox.Visibility = tag == "palbox" ? Visibility.Visible : Visibility.Collapsed;
        Audit.Visibility = tag == "audit" ? Visibility.Visible : Visibility.Collapsed;
        Craft.Visibility = tag == "craft" ? Visibility.Visible : Visibility.Collapsed;
        Breeding.Visibility = tag == "breeding" ? Visibility.Visible : Visibility.Collapsed;
        Map.Visibility = tag == "map" ? Visibility.Visible : Visibility.Collapsed;
    }
}
