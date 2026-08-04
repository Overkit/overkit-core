# Overkit Wiki

**All-in-One Overlay for Palworld** — everything inside the game, no alt-tab.

> ⚠️ Alpha software, in active development. Bugs are expected — [report them here](https://github.com/Overkit/overkit/issues).

---

## 🇬🇧 English

| Page | What's in it |
|---|---|
| **[Installation](Installation)** | Requirements and step-by-step setup |
| **[Features](Features)** | Every tool in the panel and the HUD, explained |
| **[Making Cards](Cards)** | Build your own panels — no code needed |
| **[Troubleshooting](Troubleshooting)** | Nothing shows up? Start here |

## 🇫🇷 Français

| Page | Contenu |
|---|---|
| **[Installation](Installation-FR)** | Prérequis et mise en place pas à pas |
| **[Fonctionnalités](Fonctionnalites-FR)** | Tous les outils du panneau et du HUD, expliqués |
| **[Créer des Cards](Cards-FR)** | Fabrique tes propres panneaux — sans code |
| **[Dépannage](Depannage-FR)** | Rien ne s'affiche ? Commence ici |

---

## Quick start / Démarrage rapide

1. Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) and [UE4SS](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld)
2. Download the [latest release](https://github.com/Overkit/overkit/releases), drop `PalworldMod/OverkitProbe` into `ue4ss\Mods\`
3. Run `Overkit.Host.exe`, launch Palworld in **borderless windowed**, press **F6**

## Is it safe? / Est-ce sans risque ?

**EN** — Overkit is strictly read-only: it observes the game through Unreal reflection and never writes anything back. No save edits, no gameplay calls, no network connection except a local WebSocket on `127.0.0.1`. It cannot be used to cheat, and it cannot corrupt a save.

**FR** — Overkit est strictement en lecture seule : il observe le jeu par réflexion Unreal et n'y écrit jamais rien. Aucune modification de sauvegarde, aucun appel de gameplay, aucune connexion réseau hormis un WebSocket local sur `127.0.0.1`. Il ne peut ni servir à tricher, ni corrompre une sauvegarde.
