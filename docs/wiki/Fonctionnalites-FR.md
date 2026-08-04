# Fonctionnalités

Tout ce qu'Overkit t'apporte, outil par outil. **F6** en jeu ouvre le panneau (touche remappable) ; le HUD, lui, est toujours là, discrètement.

---

## HUD — la pastille permanente

En haut à gauche, click-through (tes clics passent au jeu). Elle affiche :

- **● live / ○ hors ligne** — la sonde est-elle connectée
- **Jour et heure in-game** — `J229 13:01`
- **Coordonnées carte** — les mêmes chiffres que la carte du jeu
- **Compteur de Palbox** — `52 pals`, ou `43/64 pals*` quand le jeu n'a pas encore synchronisé toutes les pages
- **Taille de l'équipe** et, si tu en définis une, ta **cible de farm avec la distance en direct**

Le HUD se masque dès que Palworld n'est plus la fenêtre active : il ne traîne jamais par-dessus ton bureau ou une autre application.

## Palbox

Tous les Pals que tu possèdes — boîte, équipe active et travailleurs de base — dans une seule liste consultable.

- Noms d'espèces localisés, surnom, genre, niveau
- **IVs étiquetés** : `PV / MÊL / TIR / DÉF`
- Passifs, avec leurs vrais noms du jeu
- **Recherche** sur les noms, espèces et passifs ; **tri** par niveau, nom ou total des talents
- Les membres de l'équipe portent une ★

> ℹ️ Le jeu ne matérialise une page de Palbox qu'une fois que tu l'as ouverte. D'ici là, Overkit affiche un compteur honnête `X/Y synchronisés` plutôt que de prétendre que ta boîte est vide. Ouvrir la boîte une fois par session règle la question.

## Audit de base

Surveille le bien-être de chaque travailleur de chaque base et alerte avant que ça tourne mal :

- ⚠ **Avertissement** sous 50 % de faim ou de santé mentale
- 🛑 **Critique** sous 25 %

Chaque alerte nomme le Pal concerné : tu sais qui nourrir ou qui envoyer aux sources chaudes.

## Checklist de craft

Choisis une recette et une quantité. Overkit compare avec **tout ton inventaire** (sac, objets clés, boîte à nourriture) et t'indique :

- ce que tu as déjà, matériau par matériau
- **ce qui manque**, en rouge
- **quels Pals lâchent le matériau manquant** — directement depuis les tables de butin du jeu

## Accouplement inversé

Choisis le Pal que tu veux. Overkit liste **toutes les paires de parents qui le produisent**, via la formule CombiRank officielle du jeu et les combos uniques.

L'atout maître : les paires **réellement réalisables avec ta propre Palbox** — genres compris — apparaissent en tête. Bascule « Mes paires » sur off pour voir toutes les paires théoriques.

> Ce sont des possibilités, pas des certitudes : l'héritage des passifs est probabiliste et n'est pas encore calculé.

## Carte & routing de farm

Une carte stylisée avec tes bases (⌂), ta **position en direct**, et les spots de spawn de l'espèce que tu cherches :

- Points verts = spawn à toute heure · Violets = **nuit uniquement**
- Taille du point = densité de spawners regroupés
- La liste de droite est **triée par distance réelle** depuis ta position
- Clique 🎯 sur un spot et il devient ta **cible HUD** : referme le panneau, la distance défile pendant que tu cours

Le filtre jour/nuit utilise l'horloge in-game réelle : un spot nocturne ne te sera pas proposé en plein midi.

## Cards — fabrique les tiennes

Le panneau ne se limite pas à ce qui est livré avec Overkit. L'onglet **＋ Créer une card** te permet de construire tes propres panneaux en choisissant des blocs dans des listes — sans code. Voir **[Créer des Cards](Cards-FR)**.

## Icône de la zone de notification

Près de l'horloge : ouvrir le panneau, éditer les réglages (`overkit.settings.json` — port de la sonde, raccourci, noms de process du jeu), ouvrir le journal, ou quitter Overkit.
