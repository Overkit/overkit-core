# Créer des Cards

Une **Card**, c'est ton propre onglet dans le panneau. Tu la construis en choisissant des options dans des listes — sans code, sans JSON à écrire à la main.

---

## En fabriquer une dans le jeu

**F6** → **＋ Créer une card**.

1. **Nomme ta card** — par exemple *Mes reproductrices*
2. **Choisis un bloc** — « Je veux… »
   - **Compter quelque chose** — un grand chiffre, par exemple combien de femelles tu possèdes
   - **Afficher une liste** — un tableau, par exemple tous tes Pals au-dessus du niveau 40
   - **Être alerté** — un avertissement quand une condition est remplie
   - **Afficher une valeur du jeu** — jour, heure, total de pals, nombre de bases…
3. **Choisis une source** — mes Pals, mes bases, les travailleurs, les Pals autour de moi
4. **Ajoute des filtres** (facultatif) — un champ, un opérateur, une valeur, puis **+**. Les filtres se cumulent : *niveau ≥ 40* **et** *genre = female*
5. **Ajoute ce bloc à ma card** — l'**aperçu en direct**, à droite, se met à jour avec tes vraies données
6. Répète pour autant de blocs que tu veux, puis **Enregistrer**

Ta card devient un onglet immédiatement. Sans redémarrage.

## Modifier ou supprimer

Le sélecteur **Card en cours**, en haut, permet de rouvrir n'importe quelle card : son nom et ses blocs reviennent, tu ajustes, tu enregistres. Le renommage fonctionne aussi — l'onglet suit.

Le bouton **Supprimer** efface ta card et son fichier (avec confirmation).

> Les cards livrées avec Overkit portent la mention *(fournie)*. Tu peux en ouvrir une pour voir comment elle est construite ; l'enregistrer crée **ta propre copie**, qui prend le dessus sur l'originale. Elles ne sont pas supprimables — elles reviendraient à la prochaine mise à jour.

## Où vivent les cards — et pourquoi c'est important

| Emplacement | Contenu | Lors d'une mise à jour |
|---|---|---|
| `%LOCALAPPDATA%\Overkit\Cards` | Les cards que tu as créées | **Jamais touchées** |
| `<dossier d'installation>\Cards` | Les cards livrées avec Overkit | Remplacées |

Tes créations vivent hors du dossier d'installation : mettre Overkit à jour ne peut jamais les effacer. **Partager une card avec un ami revient à lui envoyer le fichier JSON** — il le dépose dans ce dossier et elle apparaît dans son panneau.

## Sous le capot : le langage d'expressions

L'éditeur écrit les expressions à ta place, mais tu peux ouvrir le JSON d'une card et l'éditer directement. Le langage est volontairement limité : pas de boucle, pas d'accès disque ou réseau, et un budget de temps par rafraîchissement — une card ne peut jamais ralentir ton jeu. Si l'une déraille, elle est suspendue avec un message indiquant quel bloc a échoué.

**Chemins** — l'état du jeu, en minuscules avec des points :

```
palbox.pals        world.time.hour      player.position.x
bases.list         nearby.actors        palbox.owned_count
```

**Filtres et agrégations**, chaînés avec `|` :

```
count(palbox.pals | where(gender = "female"))
palbox.pals | where(level >= 40)
palbox.pals | avg(talents.hp)
```

**Utilitaires** : `round`, `floor`, `abs`, `percent(a, b)`, `pad(valeur, longueur)`, `lower`, `contains`, `concat`, `if(condition, alors, sinon)`, `isset`.

Référence complète : [`docs/cards.md` dans le dépôt](https://github.com/Overkit/overkit).

## Idées à piquer

- Compter tes femelles d'une espèce que tu élèves
- Lister les Pals ayant un passif précis
- Alerter dès qu'un travailleur passe sous 30 % de santé mentale
- Afficher l'horloge in-game en `hh:mm` à côté de ton nombre de pals
