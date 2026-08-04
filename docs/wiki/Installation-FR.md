# Installation

Environ cinq minutes. Deux prérequis, puis trois copier-coller.

---

## Prérequis

- **Windows 10/11 x64**
- **[.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0/runtime)** — choisir *Exécuter des applications de bureau*. Un clic, une seule fois.
- **[UE4SS — Okaetsu experimental-palworld](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld)** — le chargeur de mods sur lequel tourne la sonde d'Overkit
- **Palworld en fenêtré sans bordure** — le plein écran exclusif masque tous les overlays, celui-ci compris

Testé sur la version **Game Pass (WinGDK)**. La version Steam devrait fonctionner — les chemins diffèrent, voir ci-dessous — mais personne ne l'a encore confirmé. [Dis-le-nous si tu essaies !](https://github.com/Overkit/overkit/issues)

## 1. Installer UE4SS (sauter si tu l'as déjà)

Télécharge `UE4SS-Palworld.zip` depuis le lien ci-dessus, puis extrais **`dwmapi.dll`** et le dossier **`ue4ss`** dans le dossier des binaires du jeu :

| Version | Dossier |
|---|---|
| Steam | `Palworld\Pal\Binaries\Win64\` |
| Game Pass | `Palworld\Content\Pal\Binaries\WinGDK\` |

> Trouver le dossier Game Pass : appli Xbox → Palworld → **⋯ Gérer** → **Fichiers** → **Parcourir**.

> ⚠️ Si tu utilises la version Steam Workshop d'UE4SS, n'installe **pas** celle-ci en plus — deux copies font planter le jeu.

## 2. Installer la sonde Overkit

Depuis la [dernière release](https://github.com/Overkit/overkit/releases), télécharge **`Overkit-Probe-….zip`** (ou récupère le mod sur Nexus Mods) et copie le dossier :

```
OverkitProbe   →   ...\ue4ss\Mods\OverkitProbe
```

Ce dossier contient la DLL de la sonde, son `mapping.json` et un `enabled.txt`. Rien d'autre à configurer.

## 3. Lancer l'overlay

Télécharge **`Overkit-Overlay-….zip`**, extrais le dossier `Overkit` où tu veux (Documents, un dossier de jeux — pas dans l'installation du jeu) et lance **`Overkit.Host.exe`**.

Il se range dans la zone de notification près de l'horloge et attend Palworld.

## 4. Jouer

Lance Palworld en **fenêtré sans bordure**, charge une partie :

- le **HUD** apparaît en haut à gauche
- **F6** ouvre le panneau interactif et libère le curseur
- **F6** à nouveau (ou la ✕) te renvoie au jeu

> ℹ️ Ouvre ta boîte à Pals une fois par session pour que le jeu synchronise toutes les pages — Overkit affiche un compteur honnête `X/Y synchronisés` d'ici là.

## Facultatif : un lanceur en un clic

Crée un fichier `.bat` à côté de ton raccourci de jeu :

```bat
@echo off
start "" "C:\chemin\vers\Overkit\Overkit.Host.exe"
start "" "steam://rungameid/1623730"
```

(Sur Game Pass : remplace la deuxième ligne par `start "" shell:AppsFolder\PocketpairInc.Palworld_ad4psfrxyesvt!AppPalShipping`.)

Un installeur qui gère tout ça est prévu.

## Désinstaller

- Supprime le dossier `Overkit`
- Supprime `...\ue4ss\Mods\OverkitProbe`
- Éventuellement `%LOCALAPPDATA%\Overkit` (tes cards et réglages)

Overkit ne modifie jamais les fichiers du jeu ni tes sauvegardes : il ne reste rien d'autre.
