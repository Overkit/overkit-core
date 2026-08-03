using Overkit.Sdk;

namespace Overkit.Host.Cards;

/// <summary>
/// Charge les Cards du dossier « Cards/ » à côté de l'exécutable : un fichier
/// JSON par Card, partageable tel quel (EXG-041). Une Card illisible est
/// signalée et ignorée — jamais de crash.
/// </summary>
public sealed class CardLoader(Action<string> log)
{
    private readonly List<CardRuntime> _cards = [];

    public IReadOnlyList<CardRuntime> Cards => _cards;

    public void LoadAll(string cardsDirectory)
    {
        if (!Directory.Exists(cardsDirectory))
        {
            Directory.CreateDirectory(cardsDirectory);
            log($"Dossier de cards créé : {cardsDirectory}");
            return;
        }

        foreach (var path in Directory.EnumerateFiles(cardsDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var definition = CardDefinition.Parse(File.ReadAllText(path));
                if (string.IsNullOrWhiteSpace(definition.Name))
                {
                    log($"Card ignorée ({Path.GetFileName(path)}) : nom manquant");
                    continue;
                }
                _cards.Add(new CardRuntime(definition, path));
                log($"Card chargée : {definition.Name} v{definition.Version}");
            }
            catch (Exception ex)
            {
                log($"Card illisible ({Path.GetFileName(path)}) : {ex.GetBaseException().Message}");
            }
        }
    }

    /// <summary>Distribue le snapshot à toutes les Cards.</summary>
    public void Dispatch(GameStateSnapshot snapshot)
    {
        foreach (var card in _cards)
        {
            card.OnStateUpdated(snapshot);
        }
    }
}
