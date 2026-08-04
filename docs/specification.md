# Spécification d'Overkit

Ce document est la référence du projet. Les commentaires du code y renvoient par leurs numéros de section (`§3.1`), par les principes (`P1`…`P7`) et par les exigences testables (`EXG-xxx`).

---

## 1. Vision et principes non négociables

Overkit est un overlay tout-en-un affiché **par-dessus** Palworld, alimenté en temps réel par l'état du jeu, extensible par des add-ons communautaires, gratuit et open source.

Sept principes, qui priment sur toute optimisation locale :

- **P1 — Lecture seule.** Aucun canal d'écriture vers le jeu, jamais. Overkit observe, il ne modifie pas. C'est ce qui garantit zéro corruption de sauvegarde, zéro conflit avec les mods de gameplay, zéro ambiguïté « cheat ».
- **P2 — Le tiers ne touche pas au process du jeu.** Un seul composant s'exécute dans Palworld : la Sonde. Les add-ons communautaires vivent exclusivement dans l'overlay.
- **P3 — Dégradation gracieuse.** Si la Sonde ne charge pas (patch, serveur refusant les mods), l'overlay bascule en mode statique pleinement fonctionnel. Le mode statique est un mode de première classe.
- **P4 — Zéro friction d'installation.** Cible : « télécharger, double-cliquer, jouer ».
- **P5 — Pas d'alt-tab.** Toute l'expérience se passe dans le jeu : HUD passif click-through + panneau interactif sur hotkey.
- **P6 — Data-driven.** Toute donnée de jeu vit dans un dataset versionné par patch, jamais en dur dans le code. Un patch du jeu = une régénération du dataset, pas une recompilation.
- **P7 — Tout est open source.** Code, dataset, outillage, documentation.

## 2. Architecture

Trois composants sur la machine du joueur :

```
┌───────────────────── PC du joueur ──────────────────────┐
│  ┌──────────────────┐        ┌───────────────────────┐  │
│  │ Palworld         │  WS    │ Overlay (.NET 8)      │  │
│  │  └ SONDE         │───────▶│  - State Bus          │  │
│  │    mod UE4SS C++ │ local  │  - Modules & Cards    │  │
│  │    lecture seule │        │  - HUD + panneau      │  │
│  └──────────────────┘        └───────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 2.1 Sonde — mod UE4SS

Rôle unique : lire l'état du jeu par réflexion Unreal et le publier sur un serveur WebSocket local. Rien d'autre : pas d'UI, pas de logique métier, pas d'écriture.

Écrite en C++ (mod UE4SS natif) : le Lua d'UE4SS n'embarque pas de socket fiable, et le transport doit être robuste. Les objets sont résolus **par nom**, jamais par offset.

Les chemins de propriétés sont externalisés dans `mapping.json`, versionné et livré avec le dataset : un patch qui renomme une propriété se corrige en éditant ce fichier, sans recompiler.

Cadences : position joueur 10 Hz · heure in-game 1 Hz · acteurs proches 2 Hz · palbox, inventaire, bases 30 s.

- `EXG-001` La Sonde n'expose aucune fonction d'écriture. La surface d'API vers le jeu est une liste blanche de lectures.
- `EXG-002` Le WebSocket n'écoute que sur `127.0.0.1`. Aucune option pour binder ailleurs.
- `EXG-003` Si un chemin de `mapping.json` ne se résout pas, le champ concerné est publié comme indisponible et la collecte continue — jamais de crash, jamais d'arrêt global.
- `EXG-004` Handshake initial : la Sonde annonce `{game_build, probe_version, mapping_version, schema_version}`.

### 2.2 Overlay — application .NET

Consomme le WebSocket, maintient le State Bus, charge le dataset, exécute les add-ons, rend le HUD et le panneau.

Deux surfaces de rendu :

- **HUD passif** : fenêtre layered topmost, click-through (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), `SetWindowPos` périodique (le jeu peut repasser devant), DPI per-monitor v2. Widgets légers, toujours visibles, jamais interactifs. Masqué quand le jeu n'est pas au premier plan.
- **Panneau interactif** : ouvert par hotkey globale (défaut `F6`, remappable). À l'ouverture, la fenêtre retire `WS_EX_TRANSPARENT`, prend le focus et libère le curseur ; à la fermeture, le focus retourne au jeu.

Prérequis documenté : Palworld en *borderless windowed* — le plein écran exclusif masque tout overlay.

- `EXG-010` Démarrage sans Sonde → mode statique automatique, mention discrète « données live indisponibles », vues statiques opérationnelles.
- `EXG-011` Reconnexion WebSocket automatique avec backoff (1 s → 30 s), transitions live↔statique sans redémarrage.
- `EXG-012` Aucune adresse mémoire, aucun offset, aucune signature AOB dans l'overlay. Il ne connaît le jeu qu'à travers le schéma du State Bus.
- `EXG-013` Consommation à vide : < 1 % CPU, < 150 Mo RAM, aucun impact FPS mesurable.
- `EXG-014` Le hotkey du panneau fonctionne même quand le jeu a le focus.

### 2.4 Dataset

Données statiques versionnées, générées par le Dumper et publiées en release : `pals.json`, `passives.json`, `breeding.json`, `items.json`, `recipes.json`, `drops.json`, `spawners.json`, plus `mapping.json` (pour la Sonde) et `map_calibration.json` (transformation monde ↔ carte).

Chaque fichier porte son `schema_version` et son `game_build`.

## 3. State Bus — le contrat central

Le State Bus est **le** contrat du projet : un add-on ne voit le jeu qu'à travers lui.

L'overlay maintient un `GameStateSnapshot` immuable, reconstruit à chaque message. Les add-ons reçoivent le snapshot, jamais une référence vivante.

### 3.1 Domaines

| Domaine | Contenu | Source |
|---|---|---|
| `player` | position, rotation, niveau, HP, stamina | live |
| `world` | heure in-game, jour/nuit, météo, monde | live |
| `inventory` | items du joueur et coffres de la base active | live |
| `palbox` | tous les Pals possédés : espèce, genre, niveau, passifs, talents | live |
| `party` | équipe active | live |
| `bases` | Pals assignés, jauges (faim, santé mentale), installations | live |
| `nearby` | acteurs Pals dans le rayon de streaming | live |
| `collectors` | statut de chaque collecteur (`ok`/`degraded`/`unavailable`) | live |
| `refdata` | accès en lecture au dataset chargé | statique |

Chaque champ est optionnel et porte un statut. Un add-on déclare les domaines qu'il requiert ; si l'un manque, il est désactivé avec un message clair plutôt que d'afficher des données fausses.

### 3.2 Versionnement

SemVer sur le schéma : ajout de champ = mineur, suppression ou renommage = majeur.

- `EXG-020` Le schéma est défini une seule fois (JSON Schema dans `schema/`) et génère les types C# de l'overlay et du SDK. Une seule source, plusieurs artefacts.

## 4. Pipeline dataset — survivre aux patchs

C'est le composant qui décide de la longévité du projet. Il doit être une commande, pas un rituel.

### 4.1 Dumper

Mod UE4SS utilitaire séparé, jamais livré aux joueurs : lancé sur une machine de dev avec le jeu chargé, il énumère les DataTables par réflexion et écrit les JSON bruts. Un builder .NET post-traite : normalisation, jointures, index.

- `EXG-030` Chaîne complète « jeu patché → dataset publié » exécutable en moins de 30 minutes, documentée.
- `EXG-031` Le diff de dataset est publié avec chaque release.

### 4.2 Calibration carte

`map_calibration.json` contient la transformation affine monde ↔ coordonnées carte. Procédure : deux points de repère éloignés, résolution du système, vérification sur des points de contrôle.

- `EXG-032` Les marqueurs souterrains sont visuellement distingués — jamais empilés silencieusement sur la surface.

## 5. Add-ons — trois niveaux d'accessibilité

Objectif : un joueur non-développeur crée un widget en dix minutes ; un développeur confirmé écrit un module complet. Aucun niveau ne peut interférer avec un autre ni avec le noyau.

### 5.1 Niveau 1 — Cards (déclaratif, zéro code)

Un fichier JSON : le créateur choisit un template de rendu (compteur, liste, jauge, alerte, texte) et le lie aux champs du State Bus via un mini-langage d'expressions sans code (chemins, filtres, comparaisons, agrégations).

Un éditeur in-game permet de composer une Card sans quitter le jeu : choisir un bloc, une source, des filtres, prévisualiser, enregistrer.

- `EXG-040` Les expressions sont évaluées par un interpréteur borné : pas de boucle, pas d'IO, pas d'allocation non bornée, budget d'évaluation par rendu — au-delà, la Card est suspendue avec un message.
- `EXG-041` Une Card se partage en un seul fichier.

### 5.2 Niveau 2 — Scripts (Lua sandboxé)

Prévu : interpréteur Lua managé, sandbox strict (pas d'`io`, pas d'`os`, pas de réseau), quotas CPU et mémoire par tick.

- `EXG-050` Un script en boucle infinie ne peut pas geler l'overlay.

### 5.3 Niveau 3 — Modules (C#)

Assemblies .NET chargées dans des `AssemblyLoadContext` collectibles, isolées. Contrat : `IOverkitModule` dans `Overkit.Sdk` — manifeste, réception du snapshot, déclaration de vues déclaratives. L'overlay possède le layout ; un module remplit des slots, il ne crée pas de fenêtres.

Les capacités sont déclarées dans le manifeste et affichées avant activation. Par défaut : aucune.

- `EXG-060` Un module qui lève une exception est désactivé et signalé ; l'overlay et les autres modules continuent.
- `EXG-061` Aucun module n'obtient de référence vers le modèle interne de l'overlay : le SDK n'expose que des types immuables.

### 5.4 Manifeste commun

```json
{
  "id": "fr.overkit.base-audit",
  "name": "Audit de base",
  "version": "1.2.0",
  "authors": ["…"],
  "license": "MIT",
  "state_requires": ["bases", "palbox"],
  "capabilities": [],
  "min_schema": "1.0"
}
```

- `EXG-070` Résolution de compatibilité au chargement (schéma, domaines requis, capacités). Tout échec rend l'add-on inactif **avec la raison affichée**, jamais silencieusement, jamais par un crash.

## 6. Modules livrés

Ils servent trois buts : valeur immédiate, mise à l'épreuve de l'API, exemples de référence.

1. **Audit de base** — aptitudes manquantes, Pals mal assignés, alertes faim et santé mentale.
2. **Checklist de craft** — recette → diff avec l'inventaire → matériaux manquants et où les farmer.
3. **Accouplement inversé** — cible → paires de parents (formule CombiRank `floor((A+B+1)/2)` + combos uniques) filtrées par la Palbox réelle, genres inclus. Les probabilités de passifs sont affichées comme probabilités, jamais comme certitudes.
4. **Routing de farm** — espèce → spots agrégés, gate jour/nuit, tri par distance, cap et distance dans le HUD.
5. **Alertes** — livré en Card, pour démontrer que le niveau 1 suffit à des choses utiles.

## 7. Stack technique

| Composant | Choix | Raison |
|---|---|---|
| Sonde | C++20, mod UE4SS, CMake | Réflexion par nom, transport fiable ; seul composant in-process |
| Transport | WebSocket local, messages JSON | Debuggable, schema-first |
| Overlay | .NET 8, C# | `AssemblyLoadContext`, écosystème NuGet |
| UI | WinUI 3 (panneau) + fenêtre HUD WinForms/Win32 | Le HUD click-through est une fenêtre séparée, plus simple à maîtriser |
| Cards | JSON + interpréteur d'expressions maison | Contrôle total du sandbox |
| Dumper | Mod UE4SS + console .NET | Réutilise la réflexion ; pipeline en une commande |

Décisions explicites : pas d'Electron ni de WebView pour le HUD ; pas de base de données côté client ; pas de compte utilisateur ; télémétrie opt-in uniquement, si elle existe un jour.

## 10. Licences

| Artefact | Licence |
|---|---|
| Sonde, overlay, SDK, Dumper, outillage | MIT |
| Datasets générés | Données transformées dérivées du jeu, propriété Pocketpair, redistribuées comme le font les wikis communautaires, retirables sur demande |
| Add-ons communautaires | Au choix de l'auteur, champ `license` obligatoire dans le manifeste |

Les assets extraits du jeu (icônes, image de carte) sont **référencés mais jamais redistribués** : l'installeur les extrait localement depuis les fichiers du joueur. C'est ce point qui distingue un projet propre d'un projet à risque.

## 11. Risques et mitigations

| Risque | Mitigation |
|---|---|
| Un patch casse la Sonde | Mode statique de première classe (P3) ; `mapping.json` sans recompilation |
| Le fork UE4SS n'est plus maintenu | Surface d'API minimale dans la Sonde ; P3 |
| Serveurs refusant les mods | P3 ; comportement documenté |
| Add-on tiers malveillant ou bogué | Hors-process (P2), lecture seule (P1), capacités déclarées, isolation |
| Dataset obsolète | Pipeline rapide ; bannière si `game_build` ≠ dataset |
| Accusation de triche | Lecture seule stricte, code ouvert, FAQ dédiée |
