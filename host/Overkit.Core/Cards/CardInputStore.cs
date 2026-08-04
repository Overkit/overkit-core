using System.Text.Json;

namespace Overkit.Host.Cards;

/// <summary>
/// Saisies des sections interactives des Cards, par identifiant de Card. Une
/// Card filtrée sur « niveau 40+ » doit retrouver son filtre au lancement
/// suivant : la valeur est donc conservée hors de la Card elle-même, pour ne
/// pas réécrire un fichier partageable à chaque frappe (EXG-041).
///
/// Le fichier vit à côté des Cards du joueur, il survit aux mises à jour.
/// </summary>
public sealed class CardInputStore(Action<string> log)
{
    private readonly Dictionary<string, Dictionary<string, string>> _values = [];

    private static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Overkit", "card-inputs.json");

    public void Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return;
            }
            var loaded = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                File.ReadAllText(Path));
            if (loaded is null)
            {
                return;
            }
            foreach (var (cardId, inputs) in loaded)
            {
                _values[cardId] = inputs;
            }
        }
        catch (Exception ex)
        {
            // Des réglages illisibles ne valent pas de bloquer les Cards : on
            // repart des valeurs par défaut.
            log($"Réglages de Cards illisibles, valeurs par défaut appliquées : {ex.GetBaseException().Message}");
        }
    }

    public string? Get(string cardId, string inputId) =>
        _values.TryGetValue(cardId, out var inputs) && inputs.TryGetValue(inputId, out var value)
            ? value
            : null;

    public void Set(string cardId, string inputId, string value)
    {
        if (!_values.TryGetValue(cardId, out var inputs))
        {
            inputs = [];
            _values[cardId] = inputs;
        }
        inputs[inputId] = value;
        Save();
    }

    /// <summary>Oublie les réglages d'une Card supprimée.</summary>
    public void Forget(string cardId)
    {
        if (_values.Remove(cardId))
        {
            Save();
        }
    }

    private void Save()
    {
        try
        {
            var path = Path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_values, SaveOptions));
        }
        catch (Exception ex)
        {
            log($"Réglages de Cards non enregistrés : {ex.GetBaseException().Message}");
        }
    }

    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };
}
