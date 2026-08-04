using Overkit.Sdk;

namespace Overkit.Host.Cards;

/// <summary>
/// Charge les Cards depuis deux emplacements distincts :
///
/// - <b>Cards fournies</b> : dossier « Cards/ » de l'installation. Elles sont
///   remplacées à chaque mise à jour d'Overkit.
/// - <b>Cards du joueur</b> : %LOCALAPPDATA%\Overkit\Cards. C'est là qu'écrit
///   l'éditeur in-game, et une mise à jour n'y touche jamais.
///
/// Une Card du joueur portant le même identifiant qu'une Card fournie prend le
/// dessus : modifier une Card livrée revient à en garder sa propre version.
/// Une Card illisible est signalée et ignorée — jamais de crash.
/// </summary>
public sealed class CardLoader(Action<string> log)
{
    private readonly List<CardRuntime> _cards = [];
    private readonly CardInputStore _inputs = new(log);
    private GameStateSnapshot _lastSnapshot = GameStateSnapshot.Empty;

    public IReadOnlyList<CardRuntime> Cards => _cards;

    /// <summary>Dossier des Cards du joueur, hors installation (survit aux mises à jour).</summary>
    public static string UserCardsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Overkit", "Cards");

    /// <summary>Signalé quand une Card est ajoutée ou remplacée à chaud (éditeur in-game).</summary>
    public event Action<CardRuntime, bool>? CardSaved;

    /// <summary>Signalé quand une Card est supprimée depuis l'éditeur.</summary>
    public event Action<CardRuntime>? CardDeleted;

    public void LoadAll(string builtInDirectory)
    {
        Directory.CreateDirectory(UserCardsDirectory);
        _inputs.Load();

        // Les Cards du joueur sont chargées en premier : elles l'emportent sur
        // une Card fournie de même identifiant.
        LoadFrom(UserCardsDirectory, isUser: true);
        LoadFrom(builtInDirectory, isUser: false);

        log($"Cards : {_cards.Count} chargée(s) — {UserCardsDirectory} (joueur) + {builtInDirectory} (fournies)");
    }

    private void LoadFrom(string directory, bool isUser)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var definition = CardDefinition.Parse(File.ReadAllText(path));
                if (string.IsNullOrWhiteSpace(definition.Name))
                {
                    log($"Card ignorée ({Path.GetFileName(path)}) : nom manquant");
                    continue;
                }
                if (_cards.Any(c => c.Definition.Id == definition.Id))
                {
                    continue; // déjà fournie par le joueur : sa version prime
                }
                _cards.Add(new CardRuntime(definition, path, _inputs) { IsUserCard = isUser });
                log($"Card chargée : {definition.Name} v{definition.Version}{(isUser ? "" : " (fournie)")}");
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

    /// <summary>
    /// Écrit une Card dans le dossier du joueur et la charge immédiatement :
    /// l'éditeur in-game n'impose pas de redémarrage, et rien n'est écrit dans
    /// le dossier d'installation.
    /// </summary>
    public CardRuntime SaveAndLoad(CardDefinition definition)
    {
        Directory.CreateDirectory(UserCardsDirectory);
        var fileName = CardBuilder.Slugify(definition.Name) + ".card.json";
        var path = Path.Combine(UserCardsDirectory, fileName);
        File.WriteAllText(path, CardBuilder.Serialize(definition));

        var runtime = new CardRuntime(definition, path, _inputs) { IsUserCard = true };
        runtime.OnStateUpdated(_lastSnapshot);

        var existing = _cards.FindIndex(c => c.Definition.Id == definition.Id);
        var replaced = existing >= 0;
        if (replaced)
        {
            // Card renommée : le fichier suit, l'ancien ne doit pas subsister.
            // Une Card fournie, elle, reste en place : la version du joueur la masque.
            var previous = _cards[existing];
            if (previous.IsUserCard &&
                !string.IsNullOrEmpty(previous.SourcePath) &&
                !string.Equals(previous.SourcePath, path, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(previous.SourcePath))
            {
                TryDeleteFile(previous.SourcePath);
            }
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

    /// <summary>
    /// Supprime une Card du joueur (fichier compris). Une Card fournie ne peut
    /// pas être supprimée : elle reviendrait à la prochaine mise à jour.
    /// </summary>
    public bool Delete(CardRuntime card)
    {
        if (!card.IsUserCard)
        {
            log($"Card fournie non supprimable : {card.Definition.Name}");
            return false;
        }
        if (!string.IsNullOrEmpty(card.SourcePath) && File.Exists(card.SourcePath) &&
            !TryDeleteFile(card.SourcePath))
        {
            return false;
        }
        _cards.Remove(card);
        _inputs.Forget(card.Definition.Id);
        log($"Card supprimée : {card.Definition.Name}");
        CardDeleted?.Invoke(card);
        return true;
    }

    private bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            log($"Fichier non supprimé ({Path.GetFileName(path)}) : {ex.GetBaseException().Message}");
            return false;
        }
    }
}
