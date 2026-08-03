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
    private DateTime _lastRender = DateTime.MinValue;

    public ModuleHostView()
    {
        InitializeComponent();
    }

    /// <summary>Vue d'un module chargé dynamiquement.</summary>
    public void Initialize(StateBus bus, ModuleLoader loader, LoadedModule module) =>
        Initialize(bus, () => loader.BuildView(module));

    /// <summary>
    /// Vue de toute source déclarative — module C# ou Card. Les deux
    /// produisent le même modèle, donc le même rendu (ADR-0007).
    /// </summary>
    public void Initialize(StateBus bus, Func<ModuleView> build)
    {
        _build = build;

        var dispatcher = DispatcherQueue;
        bus.SnapshotUpdated += _ =>
        {
            // Les modules décident de leur contenu ; on limite le rafraîchissement
            // de l'UI à 2 Hz pour rester léger.
            if ((DateTime.UtcNow - _lastRender).TotalMilliseconds < 500)
            {
                return;
            }
            _lastRender = DateTime.UtcNow;
            dispatcher.TryEnqueue(Render);
        };
        Render();
    }

    private void Render()
    {
        var view = _build();
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
                _ => null,
            };
            if (element is not null)
            {
                Root.Children.Add(element);
            }
        }
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

    private static UIElement BuildTable(TableSection section)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(BuildRow(section.Headers, header: true, null));
        foreach (var row in section.Rows)
        {
            panel.Children.Add(BuildRow(row.Cells, header: false, row.Emphasis));
        }
        return panel;

        static UIElement BuildRow(IReadOnlyList<string> cells, bool header, AlertLevel? emphasis)
        {
            var grid = new Grid { ColumnSpacing = 12, Padding = new Thickness(0, 6, 0, 6) };
            for (var i = 0; i < cells.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var text = new TextBlock
                {
                    Text = cells[i],
                    FontSize = header ? 11 : 13,
                    Opacity = header ? 0.45 : 1,
                    TextWrapping = TextWrapping.Wrap,
                };
                if (emphasis is { } level)
                {
                    text.Foreground = new SolidColorBrush(ColorFor(level));
                }
                Grid.SetColumn(text, i);
                grid.Children.Add(text);
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

    private static Windows.UI.Color ColorFor(AlertLevel level) => level switch
    {
        AlertLevel.Critical => Colors.OrangeRed,
        AlertLevel.Warning => Colors.Orange,
        _ => Colors.Gainsboro,
    };
}
