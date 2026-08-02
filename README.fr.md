# Overkit — All-in-One Overlay for Palworld

> 🇬🇧 **[English version here → README.md](README.md)**

> ⚠️ **ALPHA — en cours de développement.** Overkit évolue activement : des bugs, des manques et des changements cassants sont à prévoir. Retours et rapports de bugs bienvenus dans les [Issues](../../issues).

Overkit est un overlay tout-en-un, gratuit et open source, pour Palworld. Tout se passe **dans le jeu** — pas d'alt-tab, pas de navigateur, pas de wiki à jongler. Il lit l'état du jeu en temps réel (sans jamais y écrire) et le transforme en outils utiles.

**Lecture seule par conception.** Le composant in-game observe le jeu par réflexion et publie sur un WebSocket local (`127.0.0.1` uniquement). Aucun canal d'écriture : pas de modification de sauvegarde, pas d'appel de fonction de gameplay, pas de vecteur de triche. S'il ne peut pas se charger (patch du jeu, serveur refusant les mods), l'overlay se dégrade proprement au lieu de casser.

---

## Ce qui existe aujourd'hui (alpha)

| Outil | Ce qu'il fait |
|---|---|
| **HUD** | Pastille discrète en jeu : jour et heure in-game, coordonnées carte, compteur de Palbox, distance de la cible de farm. Click-through, masquée quand le jeu perd le focus |
| **Palbox** | Tous les Pals possédés (boîte + équipe + travailleurs) avec noms localisés, genre, niveau, **IVs étiquetés** et passifs. Recherche plein texte, tri |
| **Audit de base** | Alertes faim et santé mentale pour chaque travailleur de base (⚠ sous 50 %, 🛑 sous 25 %) |
| **Checklist de craft** | Une recette et une quantité → ce qui manque dans tout l'inventaire → quels Pals lâchent les matériaux manquants |
| **Accouplement inversé** | Un Pal cible → toutes les paires de parents (formule CombiRank officielle + les 258 combos uniques), avec les paires **réalisables avec la vraie Palbox (genres inclus)** en tête |
| **Carte & routing de farm** | Carte stylisée avec bases, position live, spots de spawn de n'importe quelle espèce (jour/nuit, agrégés, triés par distance) — envoi d'un spot en cible HUD, la distance défile en courant |

Le panneau interactif s'ouvre avec **F6** (remappable) et libère le curseur. Une icône de zone de notification (près de l'horloge) donne accès aux réglages, au journal et à la fermeture.

## Prérequis

- Windows 10/11 x64
- Palworld en **fenêtré sans bordure** (le plein écran exclusif masque tout overlay)
- [UE4SS — RE-UE4SS Okaetsu experimental-palworld](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld)
- Testé sur la version **Game Pass (WinGDK)** build `1.10.1103.0`. La version Steam devrait fonctionner (chemins différents, voir plus bas) mais reste **non testée**

## Installation

1. **Installer UE4SS** (sauter si déjà installé) :
   télécharger `UE4SS-Palworld.zip` depuis le lien ci-dessus et extraire `dwmapi.dll` + le dossier `ue4ss` dans le dossier des binaires du jeu :
   - Steam : `Palworld\Pal\Binaries\Win64\`
   - Game Pass : `Palworld\Content\Pal\Binaries\WinGDK\` (appli Xbox → Palworld → Gérer → Fichiers → Parcourir)
2. **Installer la sonde Overkit** : depuis la [dernière release](../../releases), copier le dossier `PalworldMod/OverkitProbe` dans `...\ue4ss\Mods\`.
3. **Lancer l'overlay** : extraire le dossier `Overkit` n'importe où et lancer `Overkit.Host.exe`. Il s'installe dans la zone de notification et attend le jeu.
4. Lancer Palworld (fenêtré sans bordure), charger une partie — le HUD apparaît en haut à gauche, **F6** ouvre le panneau.

> ℹ️ Ouvrir la boîte à Pals une fois par session de jeu permet au jeu de matérialiser toutes les pages — Overkit affiche un compteur honnête `X/Y synchronisés` d'ici là.

## Limitations connues (alpha)

- Les noms du dataset sont extraits d'une installation du jeu en **français** ; les autres langues viendront avec le pipeline de dataset
- Le contenu des coffres n'est pas encore détecté (le sac, les objets clés et la nourriture le sont)
- Le fond de carte est une grille stylisée — l'image officielle sera extraite localement par le futur installeur (les assets du jeu ne sont jamais redistribués)
- La complétude de la Palbox dépend de la synchro paresseuse du jeu (voir le ℹ️ ci-dessus)
- Version Steam non testée ; mode client multijoueur non testé (les données côté serveur y sont partiellement indisponibles, par conception)
- Performances : aucun impact FPS mesurable observé (~700 fps inchangés sur la machine de dev), mesure formelle P95/P99 à venir

## Compiler depuis les sources

- **Overlay (host)** : SDK .NET 8 — `dotnet build host/Overkit.Host -c Release`
- **Sonde / Dumper (mods UE4SS C++)** : Visual Studio 2026 (charge C++), CMake ≥ 3.22, Rust ≥ 1.73, et un compte GitHub lié à Epic Games (le sous-module `UEPseudo` de RE-UE4SS est verrouillé par l'EULA Epic). Voir `probe/README.md`
- **Dataset** : `dotnet run --project dataset/builder -- <dossier_raw> <dossier_out>` depuis les dumps bruts du Dumper

## Crédits & aspects légaux

- Construit sur [RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) et le [fork Palworld d'Okaetsu](https://github.com/Okaetsu/RE-UE4SS) (MIT)
- Overkit est sous licence MIT (voir [LICENSE](LICENSE))
- Les fichiers du dataset sont des données transformées dérivées de Palworld, © Pocketpair — distribuées comme le font les wikis et calculateurs communautaires, et retirables sur demande. Les assets du jeu (icônes, image de carte) ne sont jamais redistribués
- Overkit est un projet de fans, sans affiliation avec Pocketpair

État d'avancement et feuille de route : [docs/etat-avancement.md](docs/etat-avancement.md).
