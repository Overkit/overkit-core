namespace Overkit.Sdk;

public sealed record SpeciesInfo(string Id, string Name, int CombiRank, int ZukanIndex, string ZukanSuffix);

public sealed record RecipeInfo(string Key, string ProductId, int ProductCount, double WorkAmount,
                               IReadOnlyList<RecipeMaterial> Materials);

public sealed record RecipeMaterial(string ItemId, int Count);

public sealed record SpecialCombo(string ParentA, int GenderA, string ParentB, int GenderB, string Child);

public sealed record DropSource(string SpeciesId, double Rate, int Min, int Max);

/// <summary>Emplacement de spawn (coordonnées monde, cm). OnlyTime : 0 = toujours, 1 = jour, 2 = nuit.</summary>
public sealed record SpawnSpot(double X, double Y, double Z, double Radius, int OnlyTime, int LevelMin, int LevelMax);

/// <summary>
/// Données de référence du dataset (§2.4) en lecture seule : noms localisés,
/// espèces, recettes, butins, combos d'accouplement, spots de spawn. Un module
/// qui déclare la capacité `refdata` y a accès.
/// </summary>
public interface IRefData
{
    string SpeciesName(string speciesId);
    string PassiveName(string passiveId);
    string ItemName(string itemId);

    bool TryGetSpecies(string speciesId, out SpeciesInfo info);
    IReadOnlyCollection<SpeciesInfo> AllSpecies { get; }
    IReadOnlyList<RecipeInfo> Recipes { get; }
    IReadOnlyList<SpecialCombo> SpecialCombos { get; }
    IReadOnlyList<DropSource> DropSourcesFor(string itemId);
    IReadOnlyList<SpawnSpot> SpawnSpotsFor(string speciesId);
}
