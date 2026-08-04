# Créer une Card Overkit

Une Card est un **fichier JSON unique**, sans code, déposé dans le dossier `Cards/` à côté d'`Overkit.Host.exe`. Elle apparaît comme un onglet du panneau au démarrage suivant.

## Squelette

```json
{
  "id": "fr.moi.ma-card",
  "name": "Ma card",
  "version": "1.0.0",
  "authors": ["Moi"],
  "state_requires": ["palbox"],
  "sections": [ ]
}
```

`state_requires` liste les domaines du State Bus nécessaires : tant qu'ils sont indisponibles, la Card affiche un message d'attente au lieu de données fausses.

L'`id` s'écrit en reverse-DNS sur un domaine réellement détenu (`fr.moi.ma-card` pour `moi.fr`) : c'est ce qui garantit son unicité face aux autres add-ons. L'éditeur in-game, qui ne connaît aucun domaine du joueur, préfixe ses créations par `local.`.

## Sections disponibles

| `type` | Effet | Champs |
|---|---|---|
| `text` | Une ligne de contexte | `text` (les `{expressions}` y sont évaluées) |
| `counters` | Grands chiffres | `items[]` : `label`, `value` |
| `gauges` | Barres de progression | `items[]` : `label`, `current`, `max`, `warn_below` |
| `list` | Tableau | `source`, `columns[]` (`header`, `value`), `limit`, `sort_by`, `sort_desc`, `empty_text` |
| `alert` | Alerte | `level` (`info`/`warning`/`critical`), `title`, `detail`, `when`, et `for_each` + `source` pour une alerte par élément |

Toute section accepte `when` : elle n'apparaît que si l'expression est vraie.

## Le langage d'expressions

Volontairement limité : pas de boucle, pas d'appel système, pas d'accès disque ou réseau. Chaque rendu dispose d'un budget de temps et un plafond d'éléments parcourus ; au-delà, la Card est suspendue avec un message plutôt que de ralentir le jeu.

**Chemins** — les domaines du State Bus, en minuscules avec des points :

```
palbox.pals            world.time.hour        player.position.x
bases.list             inventory.containers   palbox.owned_count
```

Dans un filtre, le chemin s'applique d'abord à l'élément courant : `level`, `talents.hp`, `gender`.

**Comparaisons et logique** : `=` `!=` `<` `<=` `>` `>=`, `and`, `or`, `not`.

**Filtres et agrégations**, chaînés avec `|` :

| Fonction | Exemple |
|---|---|
| `where` | `palbox.pals \| where(gender = "female")` |
| `count` | `count(palbox.pals \| where(level >= 40))` |
| `sum` `avg` `min` `max` | `palbox.pals \| avg(talents.hp)` |
| `any` `first` | `bases.list \| any(count(workers) > 8)` |

**Fonctions utilitaires** : `round(x[, décimales])`, `floor`, `abs`, `percent(a, b)`, `lower`, `pad(valeur, longueur)`, `contains(a, b)`, `concat(...)`, `if(condition, alors, sinon)`, `isset(x)`.

## Exemples

Compter ses femelles :

```json
{ "type": "counters", "items": [
  { "label": "Femelles", "value": "count(palbox.pals | where(gender = \"female\"))" }
]}
```

Alerter sur chaque base dont un travailleur déprime :

```json
{ "type": "alert", "for_each": true, "source": "bases.list",
  "when": "count(workers | where(percent(sanity.current, sanity.max) < 30)) > 0",
  "level": "warning", "title": "Base en souffrance",
  "detail": "{count(workers | where(percent(sanity.current, sanity.max) < 30))} travailleur(s) à moins de 30 %" }
```

Lister ses meilleurs Pals :

```json
{ "type": "list", "source": "palbox.pals | where(level >= 40)",
  "sort_by": "level", "sort_desc": true, "limit": 15,
  "empty_text": "Aucun Pal de niveau 40+.",
  "columns": [
    { "header": "PAL", "value": "if(isset(nickname) and nickname != \"\", nickname, species_id)" },
    { "header": "NIVEAU", "value": "level" }
  ]}
```

La Card `Alertes` livrée avec Overkit (`cards/alertes.card.json`) combine ces trois motifs — c'est le meilleur point de départ : copie-la, renomme son `id`, modifie ses expressions.

## Diagnostic

Les erreurs de chargement et de suspension sont écrites dans `overkit.log`, à côté de l'exécutable (accessible aussi par le menu de l'icône près de l'horloge).

## Où vivent les Cards

| Emplacement | Contenu | Mise à jour d'Overkit |
|---|---|---|
| `%LOCALAPPDATA%\Overkit\Cards` | Les Cards créées avec l'éditeur in-game | **Jamais touchées** |
| `<installation>\Cards` | Les Cards fournies avec Overkit | Remplacées |

L'éditeur écrit toujours dans le dossier du joueur : une mise à jour d'Overkit ne peut donc pas effacer une Card créée. Modifier une Card fournie en crée une copie personnelle, qui prend le dessus sur l'originale.
