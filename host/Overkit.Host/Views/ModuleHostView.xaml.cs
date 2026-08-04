using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Overkit.Host.Core;
using Overkit.Sdk;

namespace Overkit.Host.Views;

/// <summary>
/// Rend une <see cref="ModuleView"/> déclarative (§5.3) : le host possède le
/// layout, le module ne fait que décrire. Ce même moteur servira les Cards
/// (niveau 1), qui produisent le même modèle de vue.
/// </summary>
public sealed partial class ModuleHostView : UserControl
{
    private Func<ModuleView> _build = null!;
    private Action<ViewInteraction>? _interact;
    private DateTime _lastRender = DateTime.MinValue;

    /// <summary>
    /// Nombre de champs de saisie ayant le focus. Un rendu reconstruit tous les
    /// contrôles : le déclencher pendant une frappe effacerait le texte en
    /// cours et perdrait le focus, donc le rafraîchissement périodique attend.
    /// </summary>
    private int _editing;

    /// <summary>
    /// Vrai pendant la construction des contrôles : les événements de sélection
    /// se déclenchent quand on restaure la valeur courante, il ne faut pas les
    /// confondre avec une action de l'utilisateur.
    /// </summary>
    private bool _building;

    public ModuleHostView()
    {
        InitializeComponent();
    }

    /// <summary>Vue d'un module chargé dynamiquement.</summary>
    public void Initialize(StateBus bus, ModuleLoader loader, LoadedModule module) =>
        Initialize(bus, () => loader.BuildView(module), interaction => loader.Interact(module, interaction));

    /// <summary>
    /// Vue de toute source déclarative — module C# ou Card. Les deux
    /// produisent le même modèle, donc le même rendu (ADR-0007).
    /// </summary>
    public void Initialize(StateBus bus, Func<ModuleView> build, Action<ViewInteraction>? interact = null)
    {
        _build = build;
        _interact = interact;

        var dispatcher = DispatcherQueue;
        bus.SnapshotUpdated += _ =>
        {
            // Les modules décident de leur contenu ; on limite le rafraîchissement
            // de l'UI à 2 Hz pour rester léger.
            if (_editing > 0 || (DateTime.UtcNow - _lastRender).TotalMilliseconds < 500)
            {
                return;
            }
            _lastRender = DateTime.UtcNow;
            dispatcher.TryEnqueue(Render);
        };
        Render();
    }

    /// <summary>
    /// Remonte une action au module et réaffiche aussitôt : c'est le module qui
    /// décide de ce que l'action change, le host ne fait que le lui demander.
    /// </summary>
    private void Send(string id, string value)
    {
        if (_building || _interact is null)
        {
            return;
        }
        _interact(new ViewInteraction(id, value));
        _lastRender = DateTime.UtcNow;
        Render();
    }

    private void Render()
    {
        var view = _build();
        _building = true;
        Root.Children.Clear();

        foreach (var section in view.Sections)
        {
            var element = section switch
            {
                StatusSection status => BuildStatus(status),
                EmptySection empty => BuildEmpty(empty),
                AlertsSection alerts => BuildAlerts(alerts),
                TableSection table => BuildTable(table),
                GaugesSection gauges => BuildGauges(gauges),
                CountersSection counters => BuildCounters(counters),
                TextInputSection text => BuildTextInput(text),
                NumberInputSection number => BuildNumberInput(number),
                ChoiceSection choice => BuildChoice(choice),
                ToggleSection toggle => BuildToggle(toggle),
                ActionsSection actions => BuildActions(actions),
                _ => null,
            };
            if (element is not null)
            {
                Root.Children.Add(element);
            }
        }

        _building = false;
    }

    private static UIElement BuildStatus(StatusSection section) =>
        new TextBlock { Text = section.Text, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };

    private static UIElement BuildEmpty(EmptySection section) =>
        new TextBlock { Text = section.Message, Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private static UIElement BuildAlerts(AlertsSection section)
    {
        var list = new StackPanel { Spacing = 2 };
        foreach (var item in section.Items)
        {
            var row = new Grid { Padding = new Thickness(0, 8, 0, 8), ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            var glyph = new TextBlock
            {
                Text = item.Level switch
                {
                    AlertLevel.Critical => "🛑",
                    AlertLevel.Warning => "⚠",
                    _ => "•",
                },
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var title = new TextBlock
            {
                Text = item.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            var detail = new TextBlock
            {
                Text = item.Detail,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(ColorFor(item.Level)),
            };
            Grid.SetColumn(title, 1);
            Grid.SetColumn(detail, 2);
            row.Children.Add(glyph);
            row.Children.Add(title);
            row.Children.Add(detail);
            list.Children.Add(row);
        }
        return list;
    }

    private UIElement BuildTable(TableSection section)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(BuildRow(section.Headers.Select(h => new TableCell(h)).ToList(), header: true, null));
        foreach (var row in section.Rows)
        {
            var cells = BuildRow(row.Cells, header: false, row.Emphasis);

            // Sans identifiant de sélection, le tableau reste une simple grille :
            // pas de bouton, donc pas de cible cliquable ni de survol.
            if (section.SelectionId is null || row.Key is null)
            {
                panel.Children.Add(cells);
                continue;
            }

            var key = row.Key;
            var button = new Button
            {
                Content = cells,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            if (row.Selected)
            {
                button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
            }
            button.Click += (_, _) => Send(section.SelectionId, key);
            panel.Children.Add(button);
        }
        return panel;

        static UIElement BuildRow(IReadOnlyList<TableCell> cells, bool header, AlertLevel? rowEmphasis)
        {
            var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 6, 0, 6) };
            for (var i = 0; i < cells.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var text = new TextBlock
                {
                    Text = cells[i].Text,
                    FontSize = header ? 11 : 13,
                    Opacity = header ? 0.45 : 1,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                // La cellule l'emporte sur la ligne : colorer un seul chiffre
                // manquant est plus lisible que teindre toute la ligne.
                if ((cells[i].Emphasis ?? rowEmphasis) is { } level)
                {
                    text.Foreground = new SolidColorBrush(ColorFor(level));
                }

                FrameworkElement content = text;
                if (cells[i].Secondary is { Length: > 0 } secondary)
                {
                    var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    stack.Children.Add(text);
                    stack.Children.Add(new TextBlock
                    {
                        Text = secondary,
                        FontSize = 12,
                        Opacity = 0.55,
                        TextWrapping = TextWrapping.Wrap,
                    });
                    content = stack;
                }

                Grid.SetColumn(content, i);
                grid.Children.Add(content);
            }
            return grid;
        }
    }

    private static UIElement BuildGauges(GaugesSection section)
    {
        var panel = new StackPanel { Spacing = 10 };
        foreach (var gauge in section.Items)
        {
            var percent = gauge.Max > 0 ? gauge.Current / gauge.Max * 100 : 0;
            var block = new StackPanel { Spacing = 4 };
            block.Children.Add(new TextBlock
            {
                Text = $"{gauge.Label} — {gauge.Current:F0}/{gauge.Max:F0}",
                FontSize = 13,
            });
            block.Children.Add(new ProgressBar
            {
                Value = Math.Clamp(percent, 0, 100),
                Maximum = 100,
                Foreground = gauge.Emphasis is { } level ? new SolidColorBrush(ColorFor(level)) : null,
            });
            panel.Children.Add(block);
        }
        return panel;
    }

    private static UIElement BuildCounters(CountersSection section)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        foreach (var counter in section.Items)
        {
            var block = new StackPanel();
            block.Children.Add(new TextBlock
            {
                Text = counter.Value,
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            block.Children.Add(new TextBlock { Text = counter.Label, FontSize = 11, Opacity = 0.55 });
            panel.Children.Add(block);
        }
        return panel;
    }

    private UIElement BuildTextInput(TextInputSection section)
    {
        var box = new TextBox
        {
            Header = string.IsNullOrEmpty(section.Label) ? null : section.Label,
            Text = section.Value,
            PlaceholderText = section.Placeholder,
        };

        // La saisie n'est remontée qu'une fois validée : à chaque frappe, le
        // module reconstruirait la vue et le champ perdrait le focus.
        box.GotFocus += (_, _) => _editing++;
        box.LostFocus += (_, _) =>
        {
            _editing--;
            if (box.Text != section.Value)
            {
                Send(section.Id, box.Text);
            }
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter && box.Text != section.Value)
            {
                e.Handled = true;
                Send(section.Id, box.Text);
            }
        };
        return box;
    }

    private UIElement BuildNumberInput(NumberInputSection section)
    {
        var box = new NumberBox
        {
            Header = string.IsNullOrEmpty(section.Label) ? null : section.Label,
            Value = section.Value,
            Minimum = section.Min,
            Maximum = section.Max,
            SmallChange = section.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 160,
        };
        box.GotFocus += (_, _) => _editing++;
        box.LostFocus += (_, _) => _editing--;

        // NumberBox ne notifie qu'à la validation (entrée, perte de focus,
        // flèches), il n'y a donc rien de plus à retenir ici.
        box.ValueChanged += (_, e) =>
        {
            if (!double.IsNaN(e.NewValue) && Math.Abs(e.NewValue - section.Value) > double.Epsilon)
            {
                Send(section.Id, e.NewValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        };
        return box;
    }

    private UIElement BuildChoice(ChoiceSection section)
    {
        var combo = new ComboBox
        {
            Header = string.IsNullOrEmpty(section.Label) ? null : section.Label,
            ItemsSource = section.Options.Select(option => option.Label).ToList(),
            MinWidth = 200,
        };
        var index = section.SelectedValue is null
            ? -1
            : section.Options.ToList().FindIndex(option => option.Value == section.SelectedValue);
        combo.SelectedIndex = index;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < section.Options.Count)
            {
                Send(section.Id, section.Options[combo.SelectedIndex].Value);
            }
        };
        return combo;
    }

    private UIElement BuildToggle(ToggleSection section)
    {
        var toggle = new ToggleSwitch { Header = section.Label, IsOn = section.Value };
        toggle.Toggled += (_, _) => Send(section.Id, toggle.IsOn ? "true" : "false");
        return toggle;
    }

    private UIElement BuildActions(ActionsSection section)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var item in section.Items)
        {
            var button = new Button { Content = item.Label };
            if (item.IsPrimary)
            {
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            }
            button.Click += (_, _) => Send(item.Id, "");
            panel.Children.Add(button);
        }
        return panel;
    }

    private static Windows.UI.Color ColorFor(AlertLevel level) => level switch
    {
        AlertLevel.Critical => Colors.OrangeRed,
        AlertLevel.Warning => Colors.Orange,
        _ => Colors.Gainsboro,
    };
}
