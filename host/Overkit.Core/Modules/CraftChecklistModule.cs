using Overkit.Contracts;
using Overkit.Host.Core;

namespace Overkit.Host.Modules;

public sealed record ChecklistLine(string ItemId, string ItemName, int Needed, int Have, int Missing, string Sources);

public sealed record Checklist(RecipeInfo Recipe, int Quantity, IReadOnlyList<ChecklistLine> Lines, bool Complete);

/// <summary>
/// Module Checklist de craft (§6.2) : recette choisie → diff avec l'inventaire
/// (sac + coffres) → matériaux manquants → où farmer (espèces qui lâchent
/// l'objet, via le dataset drops).
/// </summary>
public static class CraftChecklistModule
{
    public static Checklist Compute(RecipeInfo recipe, int quantity, GameStateSnapshot snapshot, RefData refData)
    {
        // Stock total par objet, tous conteneurs confondus (sac, clés,
        // nourriture, coffres).
        var stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Inventory?.Containers is { } containers)
        {
            foreach (var container in containers)
            {
                if (container.Slots is null)
                {
                    continue;
                }
                foreach (var slot in container.Slots)
                {
                    stock[slot.Item_id] = stock.GetValueOrDefault(slot.Item_id) + slot.Count;
                }
            }
        }

        var lines = new List<ChecklistLine>();
        var complete = true;
        foreach (var (itemId, count) in recipe.Materials)
        {
            var needed = count * quantity;
            var have = stock.GetValueOrDefault(itemId);
            var missing = Math.Max(0, needed - have);
            if (missing > 0)
            {
                complete = false;
            }

            var sources = "";
            if (missing > 0)
            {
                var drops = refData.DropSourcesFor(itemId);
                if (drops.Count > 0)
                {
                    sources = "Lâché par : " + string.Join(", ",
                        drops.Take(3).Select(d => refData.SpeciesName(d.SpeciesId)));
                }
            }

            lines.Add(new ChecklistLine(itemId, refData.ItemName(itemId), needed, have, missing, sources));
        }

        return new Checklist(recipe, quantity, lines, complete);
    }
}
