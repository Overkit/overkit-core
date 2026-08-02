using Overkit.Contracts;
using Overkit.Host.Core;

namespace Overkit.Host.Modules;

public enum FindingSeverity
{
    Warning,
    Critical,
}

public sealed record AuditFinding(FindingSeverity Severity, string PalName, string Gauge, double Percent, string Detail);

/// <summary>
/// Module Audit de base (§6.1), tranche bien-être : alerte sur la faim et la
/// santé mentale des travailleurs de chaque base. Fonction pure sur le
/// snapshot — aucune dépendance UI, aucune écriture.
/// </summary>
public static class BaseAuditModule
{
    private const double WarningThreshold = 50;
    private const double CriticalThreshold = 25;

    public static List<AuditFinding> Analyze(GameStateSnapshot snapshot, RefData refData)
    {
        var findings = new List<AuditFinding>();
        if (snapshot.Bases?.List is not { Count: > 0 } bases)
        {
            return findings;
        }

        // Jointure instance_id -> Pal du domaine palbox (qui inclut les
        // travailleurs de base) pour un nom affichable.
        var byInstance = new Dictionary<string, Pal>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Palbox?.Pals is { } pals)
        {
            foreach (var pal in pals)
            {
                byInstance[pal.Instance_id] = pal;
            }
        }

        foreach (var baseInfo in bases)
        {
            if (baseInfo.Workers is null)
            {
                continue;
            }
            foreach (var worker in baseInfo.Workers)
            {
                var name = DisplayName(worker.Instance_id, byInstance, refData);
                Check(findings, name, "faim", worker.Hunger);
                Check(findings, name, "santé mentale", worker.Sanity);
            }
        }

        findings.Sort((a, b) => a.Severity == b.Severity
            ? a.Percent.CompareTo(b.Percent)
            : b.Severity.CompareTo(a.Severity));
        return findings;
    }

    private static void Check(List<AuditFinding> findings, string palName, string gauge, Gauge? value)
    {
        if (value is null || value.Max <= 0)
        {
            return;
        }
        var percent = value.Current / value.Max * 100.0;
        if (percent >= WarningThreshold)
        {
            return;
        }
        findings.Add(new AuditFinding(
            percent < CriticalThreshold ? FindingSeverity.Critical : FindingSeverity.Warning,
            palName, gauge, percent,
            $"{gauge} à {percent:F0} % ({value.Current:F0}/{value.Max:F0})"));
    }

    private static string DisplayName(string instanceId, Dictionary<string, Pal> byInstance, RefData refData)
    {
        if (byInstance.TryGetValue(instanceId, out var pal))
        {
            return string.IsNullOrWhiteSpace(pal.Nickname)
                ? refData.SpeciesName(pal.Species_id)
                : $"{pal.Nickname} ({refData.SpeciesName(pal.Species_id)})";
        }
        return "Pal " + instanceId[..Math.Min(8, instanceId.Length)];
    }
}
