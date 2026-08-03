using Overkit.Sdk;
using Overkit.Contracts;
using Overkit.Host.Core;

namespace Overkit.Host.Modules;

public sealed record BreedingPair(
    string ParentAId, string ParentAName,
    string ParentBId, string ParentBName,
    bool Owned, bool IsSpecial, string Note);

public sealed record BreedingResult(string TargetId, string TargetName, IReadOnlyList<BreedingPair> Pairs, int TotalPairs);

/// <summary>
/// Module Accouplement inversé (§6.3) : cible → toutes les paires de parents
/// (formule CombiRank floor((A+B+1)/2) + combos spéciaux du dataset) →
/// paires réalisables avec la Palbox réelle (genres inclus) en tête.
/// Probabilités et passifs : à venir — les résultats sont des possibilités,
/// jamais des certitudes.
/// </summary>
public static class BreedingModule
{
    public static BreedingResult FindPairs(string targetId, GameStateSnapshot snapshot, RefData refData)
    {
        var pairs = new List<BreedingPair>();
        var owned = OwnedGenders(snapshot);

        // Enfants « uniques » : ne naissent que de leur combo spécial.
        var uniqueChildren = new HashSet<string>(
            refData.SpecialCombos.Select(c => c.Child), StringComparer.OrdinalIgnoreCase);

        // 1. Combos spéciaux menant à la cible.
        foreach (var combo in refData.SpecialCombos)
        {
            if (!combo.Child.Equals(targetId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            pairs.Add(MakePair(combo.ParentA, combo.ParentB, owned, refData, isSpecial: true,
                               genderA: combo.GenderA, genderB: combo.GenderB));
        }

        // 2. Formule CombiRank — uniquement si la cible n'est pas un enfant unique.
        if (!uniqueChildren.Contains(targetId) &&
            refData.TryGetSpecies(targetId, out var target) && target.ZukanIndex > 0)
        {
            // Pool des enfants possibles par rang : espèces au Zukan hors uniques.
            var pool = refData.AllSpecies
                .Where(s => s.ZukanIndex > 0 && !uniqueChildren.Contains(s.Id))
                .OrderBy(s => s.CombiRank)
                .ThenBy(s => s.ZukanIndex)
                .ToArray();

            var parents = refData.AllSpecies.Where(s => s.ZukanIndex > 0).ToArray();
            for (var i = 0; i < parents.Length; i++)
            {
                for (var j = i; j < parents.Length; j++)
                {
                    var childRank = (parents[i].CombiRank + parents[j].CombiRank + 1) / 2;
                    if (NearestByRank(pool, childRank).Id.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        pairs.Add(MakePair(parents[i].Id, parents[j].Id, owned, refData,
                                           isSpecial: false, genderA: 0, genderB: 0));
                    }
                }
            }
        }

        // Paires réalisables avec la Palbox d'abord, puis alphabétique.
        var ordered = pairs
            .OrderByDescending(p => p.Owned)
            .ThenBy(p => p.ParentAName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.ParentBName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        const int displayCap = 250;
        return new BreedingResult(
            targetId, refData.SpeciesName(targetId),
            ordered.Take(displayCap).ToList(), ordered.Count);
    }

    private static SpeciesInfo NearestByRank(SpeciesInfo[] poolSortedByRank, int rank)
    {
        // Recherche binaire du rang le plus proche ; égalité → premier dans
        // l'ordre (rang puis Zukan), conforme à l'usage communautaire.
        var lo = 0;
        var hi = poolSortedByRank.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (poolSortedByRank[mid].CombiRank < rank)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        var best = poolSortedByRank[lo];
        if (lo > 0)
        {
            var below = poolSortedByRank[lo - 1];
            if (Math.Abs(below.CombiRank - rank) <= Math.Abs(best.CombiRank - rank))
            {
                best = below;
            }
        }
        return best;
    }

    private static BreedingPair MakePair(string idA, string idB, Dictionary<string, (bool Male, bool Female)> owned,
                                         RefData refData, bool isSpecial, int genderA, int genderB)
    {
        var feasible = IsFeasible(idA, idB, genderA, genderB, owned);
        var note = isSpecial ? "combo unique" : "";
        if (genderA != 0 || genderB != 0)
        {
            note += (note.Length > 0 ? " · " : "") + "genres imposés";
        }
        return new BreedingPair(idA, refData.SpeciesName(idA), idB, refData.SpeciesName(idB),
                                feasible, isSpecial, note);
    }

    private static bool IsFeasible(string idA, string idB, int genderA, int genderB,
                                   Dictionary<string, (bool Male, bool Female)> owned)
    {
        if (!owned.TryGetValue(idA, out var a) || !owned.TryGetValue(idB, out var b))
        {
            return false;
        }
        if (genderA == 1 && genderB == 2)
        {
            return a.Male && b.Female;
        }
        if (genderA == 2 && genderB == 1)
        {
            return a.Female && b.Male;
        }
        if (idA.Equals(idB, StringComparison.OrdinalIgnoreCase))
        {
            return a.Male && a.Female;
        }
        return (a.Male && b.Female) || (a.Female && b.Male);
    }

    private static Dictionary<string, (bool Male, bool Female)> OwnedGenders(GameStateSnapshot snapshot)
    {
        var owned = new Dictionary<string, (bool Male, bool Female)>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Palbox?.Pals is not { } pals)
        {
            return owned;
        }
        foreach (var pal in pals)
        {
            var entry = owned.GetValueOrDefault(pal.Species_id);
            if (pal.Gender == PalGender.Male)
            {
                entry.Male = true;
            }
            if (pal.Gender == PalGender.Female)
            {
                entry.Female = true;
            }
            owned[pal.Species_id] = entry;
        }
        return owned;
    }
}
