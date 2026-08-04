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
        _lastSnapshot = snapshot;
        foreach (var card in _cards)
        {
            card.OnStateUpdated(snapshot);
        }
    }

    private GameStateSnapshot _lastSnapshot = GameStateSnapshot.Empty;

    /// <summary>Signalé quand une Card est ajoutée ou remplacée à chaud (éditeur in-game).</summary>
    public event Action<CardRuntime, bool>? CardSaved;

    /// <summary>
    /// Écrit une Card dans le dossier et la charge immédiatement : l'éditeur
    /// in-game n'impose pas de redémarrage. Retourne l'exécution prête à
    /// afficher, et si elle a remplacé une Card existante.
    /// </summary>
    public CardRuntime SaveAndLoad(CardDefinition definition, string cardsDirectory)
    {
        Directory.CreateDirectory(cardsDirectory);
        var fileName = CardBuilder.Slugify(definition.Name) + ".card.json";
        var path = Path.Combine(cardsDirectory, fileName);
        File.WriteAllText(path, CardBuilder.Serialize(definition));

        var runtime = new CardRuntime(definition, path);
        runtime.OnStateUpdated(_lastSnapshot);

        var existing = _cards.FindIndex(c => c.Definition.Id == definition.Id);
        var replaced = existing >= 0;
        if (replaced)
        {
            _cards[existing] = runtime;
        }
        else
        {
            _cards.Add(runtime);
        }

        log($"Card {(replaced ? "mise à jour" : "créée")} : {definition.Name} → {fileName}");
        CardSaved?.Invoke(runtime, replaced);
        return runtime;
    }
}
