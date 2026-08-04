# Créer un module Overkit

Un module est une **DLL .NET 8** déposée dans le dossier `Modules/` à côté d'`Overkit.Host.exe`. Il reçoit l'état du jeu et décrit une vue ; l'overlay la rend. C'est le niveau 2 de l'extensibilité — pour un add-on sans code, voir [les Cards](cards.md).

Un module ne peut ni écrire dans le jeu (P1, lecture seule), ni créer de fenêtre (l'overlay possède le layout), ni voir le modèle interne du host. Une exception levée par un module le désactive avec un message sur son onglet, sans toucher à l'overlay ni aux autres modules.

## Démarrer

```bash
dotnet new install Overkit.Templates
dotnet new overkit-module -n MonModule --moduleName "Mon module" --moduleAuthor "Moi" --idPrefix fr
cd MonModule
dotnet build -c Release
```

`--idPrefix` est le domaine de premier niveau de l'identifiant : l'identifiant s'écrit en reverse-DNS sur un domaine réellement détenu (`fr.moi.mon-module` pour `moi.fr`), c'est ce qui garantit son unicité face aux autres modules chargés.

Le projet produit une DLL qui ne recopie ni `Overkit.Sdk.dll` ni `Overkit.Contracts.dll` : c'est l'overlay qui les fournit, et charger sa propre copie provoquerait un conflit de types.

## Installer

```
Overkit\Modules\mon-module\MonModule.dll
```

Un sous-dossier par module, redémarrage de l'overlay, l'onglet apparaît. Les problèmes de chargement — domaine d'état inconnu, schéma trop récent, exception au démarrage — sont écrits dans `overkit.log` et affichés sur l'onglet.

## Le manifeste

```csharp
public ModuleManifest Manifest { get; } = new()
{
    Id = "fr.moi.mon-module",
    Name = "Mon module",
    Version = "1.0.0",
    Authors = ["Moi"],
    License = "MIT",
    StateRequires = ["palbox"],
    StateOptional = ["bases"],
    Capabilities = ["refdata"],
    MinSchema = "1.0",
};
```

| Champ | Rôle |
|---|---|
| `StateRequires` | Domaines sans lesquels le module ne peut rien faire. Tant qu'ils sont indisponibles, l'overlay affiche un message d'attente plutôt que des données fausses. |
| `StateOptional` | Domaines exploités s'ils sont là. |
| `Capabilities` | `refdata` pour l'accès au dataset, `storage` pour un espace de persistance. Non déclarée, la capacité n'est pas fournie : `Context.RefData` vaut `null`. |
| `MinSchema` | Version minimale du State Bus supportée. Un module qui demande plus que ce que fournit l'overlay est désactivé avec la raison affichée. |

Les domaines connus sont `player`, `world`, `inventory`, `palbox`, `party`, `bases`, `nearby`, `collectors`. Un nom inconnu est un refus de chargement, pas un avertissement.

## Le cycle de vie

```csharp
public void Initialize(IModuleContext context);      // une fois
public void OnStateUpdated(GameStateSnapshot state); // à chaque snapshot
public ModuleView BuildView();                       // quand il faut afficher
public void OnInteraction(ViewInteraction action);   // sur action de l'utilisateur
```

`OnStateUpdated` est appelé à la cadence de la sonde : il doit rester bref, sans entrée/sortie ni attente. Le calcul lourd a sa place dans `BuildView`, appelé seulement quand l'onglet est visible et au plus deux fois par seconde.

Le snapshot est immuable et complet : le conserver tel quel dans un champ suffit, il n'y a rien à copier.

## Lire l'état

```csharp
if (_snapshot.Palbox?.Pals is not { Count: > 0 } pals)
{
    return new ModuleView(Manifest.Name, [
        new EmptySection(_snapshot.Mode == ConnectionMode.Static
            ? "Données live indisponibles — la sonde n'est pas connectée."
            : "En attente des données de la Palbox."),
    ]);
}
```

Un domaine à `null` ou vide ne veut pas dire « il n'y a rien » mais « la sonde n'a pas pu le lire ». `snapshot.IsUsable("palbox")` et `snapshot.StatusOf("palbox")` distinguent les deux ; `Mode` dit si la sonde est connectée du tout.

Avec la capacité `refdata`, `Context.RefData` traduit les identifiants du jeu : `SpeciesName`, `ItemName`, `PassiveName`, plus les recettes, les butins, les combos d'accouplement et les spots de spawn.

## Décrire une vue

Le module ne construit aucun contrôle : il retourne une liste de sections.

| Section | Contenu |
|---|---|
| `StatusSection` | Ligne de contexte en tête de vue |
| `EmptySection` | Message quand il n'y a rien à montrer |
| `CountersSection` | Grands chiffres (`CounterItem`) |
| `GaugesSection` | Barres valeur/maximum (`GaugeItem`) |
| `AlertsSection` | Alertes triées par gravité (`AlertItem`) |
| `TableSection` | En-têtes + lignes (`TableRow`) |

```csharp
return new ModuleView(Manifest.Name, [
    new StatusSection($"{pals.Count} Pals dans la boîte"),
    new TableSection(["PAL", "NIVEAU"], rows),
]);
```

`AlertLevel` (`Info`, `Warning`, `Critical`) colore une alerte, une jauge, une ligne de tableau (`TableRow.Emphasis`) ou une cellule seule.

Une cellule est une chaîne dans le cas courant — `new TableRow(["Lamball", "12"])` — et un `TableCell` quand il faut colorer cette cellule-là ou lui adjoindre une ligne secondaire :

```csharp
new TableRow([
    new TableCell(nickname, Secondary: species),          // espèce sous le surnom
    new TableCell($"{have}/{needed}", AlertLevel.Critical), // le chiffre qui manque, en rouge
])
```

L'emphase de la cellule l'emporte sur celle de la ligne.

## Sections interactives

Le module déclare un champ avec sa **valeur courante** ; l'overlay le rend, et lui renvoie l'action par `OnInteraction`. L'état du champ vit dans le module, pas dans le host.

| Section | Valeur reçue |
|---|---|
| `TextInputSection` | Le texte, à la validation (entrée ou perte de focus) |
| `NumberInputSection` | Le nombre, en invariant — `interaction.AsNumber()` |
| `ChoiceSection` | Le `Value` de l'option choisie |
| `ToggleSection` | `"true"` ou `"false"` — `interaction.AsBool()` |
| `ActionsSection` | Chaîne vide : seul l'identifiant du bouton compte |
| `TableSection` | Avec `SelectionId`, les lignes portant une `Key` deviennent cliquables |

```csharp
private string _search = "";

public void OnInteraction(ViewInteraction interaction)
{
    if (interaction.Id == "search")
    {
        _search = interaction.Value;
    }
}

// dans BuildView
new TextInputSection("search", "Rechercher", _search, "nom d'espèce"),
```

L'identifiant d'une section doit être stable d'un rendu à l'autre : c'est lui qui relie le contrôle affiché à la valeur reçue.

La saisie texte n'est remontée qu'à la validation, et le rafraîchissement périodique s'interrompt tant qu'un champ a le focus : un rendu reconstruit tous les contrôles, le déclencher pendant une frappe effacerait le texte en cours.

## Diagnostic

`Context.Log("…")` écrit dans `overkit.log`, préfixé par l'identifiant du module. Le journal est accessible depuis le menu de l'icône près de l'horloge.

## Publier

Le module de référence livré séparément est [`modules/Overkit.Module.BaseAudit`](../modules/Overkit.Module.BaseAudit) : audit de faim et de santé mentale, avec seuil réglable et filtre par base. C'est le meilleur point de départ après le template.

Les onglets Palbox et Craft sont eux aussi des modules déclaratifs ([`host/Overkit.Core/Modules`](../host/Overkit.Core/Modules)), enregistrés au démarrage au lieu d'être chargés depuis une DLL. Ils passent par le même contrat : ce qu'ils font, un module tiers peut le faire.

Un dépôt de registre recensera les modules communautaires, publication par pull request avec validation du manifeste et de la licence en CI.
