using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Overkit.Host.Core;
using Overkit.Host.Modules;

namespace Overkit.Host.Views;

public sealed class AuditRow
{
    public string Glyph { get; set; } = "";
    public string PalName { get; set; } = "";
    public string Detail { get; set; } = "";
    public Brush DetailBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Gainsboro);
}

/// <summary>Vue du module Audit de base : alertes bien-être des travailleurs.</summary>
public sealed partial class AuditView : UserControl
{
    private StateBus _bus = null!;
    private RefData _refData = null!;
    private object? _lastBases;

    public ObservableCollection<AuditRow> Findings { get; } = [];

    public AuditView()
    {
        InitializeComponent();
        FindingList.ItemsSource = Findings;
    }

    public void Initialize(StateBus bus, RefData refData)
    {
        _bus = bus;
        _refData = refData;
        var dispatcher = DispatcherQueue;
        _bus.SnapshotUpdated += snapshot =>
        {
            if (!ReferenceEquals(snapshot.Bases, _lastBases))
            {
                dispatcher.TryEnqueue(Refresh);
            }
        };
        Refresh();
    }

    private void Refresh()
    {
        var snapshot = _bus.Current;
        _lastBases = snapshot.Bases;

        Findings.Clear();
        if (snapshot.Bases?.List is not { Count: > 0 })
        {
            StatusText.Text = snapshot.Mode == ConnectionMode.Static
                ? "données live indisponibles"
                : "aucune base détectée pour l'instant";
            return;
        }

        var findings = BaseAuditModule.Analyze(snapshot, _refData);
        var workers = snapshot.Bases.List.Sum(b => b.Workers?.Count ?? 0);

        if (findings.Count == 0)
        {
            StatusText.Text = $"{workers} travailleurs surveillés — tout va bien ✓";
            return;
        }

        var critical = findings.Count(f => f.Severity == FindingSeverity.Critical);
        StatusText.Text = critical > 0
            ? $"{findings.Count} alertes dont {critical} critiques sur {workers} travailleurs"
            : $"{findings.Count} alertes sur {workers} travailleurs";

        foreach (var finding in findings)
        {
            Findings.Add(new AuditRow
            {
                Glyph = finding.Severity == FindingSeverity.Critical ? "🛑" : "⚠",
                PalName = finding.PalName,
                Detail = finding.Detail,
                DetailBrush = new SolidColorBrush(finding.Severity == FindingSeverity.Critical
                    ? Microsoft.UI.Colors.OrangeRed
                    : Microsoft.UI.Colors.Orange),
            });
        }
    }
}
