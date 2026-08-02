# Overkit — All-in-One Overlay for Palworld

> 🇫🇷 **[Version française ici / French version here → README.fr.md](README.fr.md)**

> ⚠️ **ALPHA — work in progress.** Overkit is in active development. Bugs, missing features and breaking changes are expected. Feedback and bug reports are welcome in the [Issues](../../issues).

Overkit is a free, open-source, all-in-one overlay for Palworld. Everything happens **inside the game** — no alt-tab, no browser, no wiki juggling. It reads the live state of the game (never writes to it) and turns it into useful tools.

**Read-only by design.** The in-game component observes the game through reflection and publishes to a local WebSocket (`127.0.0.1` only). It contains no write path: no save edits, no gameplay function calls, no cheating vector. If it can't load (game patch, modded server restrictions), the overlay degrades gracefully instead of breaking.

---

## What exists today (alpha)

| Tool | What it does |
|---|---|
| **HUD** | Discreet in-game pill: in-game day & time, map coordinates, palbox count, farm target distance. Click-through, hidden when the game loses focus |
| **Palbox browser** | Every owned Pal (box + party + base workers) with localized names, gender, level, **IVs (labeled)** and passive skills. Full-text search, sorting |
| **Base audit** | Hunger and sanity alerts for every base worker (⚠ below 50 %, 🛑 below 25 %) |
| **Craft checklist** | Pick a recipe and a quantity → what's missing across the whole inventory → which Pals drop the missing materials |
| **Reverse breeding** | Pick a target Pal → all parent pairs (official CombiRank formula + the 258 unique combos), with pairs **achievable with the actual palbox (genders included)** listed first |
| **Map & farm routing** | Stylized map with bases, live player position, spawn spots of any species (day/night aware, clustered, sorted by distance) — send a spot to the HUD as a target and watch the distance tick down while running |

The interactive panel opens with **F6** (rebindable) and frees the mouse cursor. A tray icon (next to the clock) provides settings, log access and exit.

## Requirements

- Windows 10/11 x64
- Palworld in **borderless windowed** mode (exclusive fullscreen hides any overlay)
- [UE4SS — RE-UE4SS Okaetsu experimental-palworld](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld)
- Tested on the **Game Pass (WinGDK)** build `1.10.1103.0`. The Steam build should work (paths differ, see below) but is **untested** so far

## Installation

1. **Install UE4SS** (skip if already installed):
   download `UE4SS-Palworld.zip` from the link above and extract `dwmapi.dll` + the `ue4ss` folder into the game's binaries folder:
   - Steam: `Palworld\Pal\Binaries\Win64\`
   - Game Pass: `Palworld\Content\Pal\Binaries\WinGDK\` (Xbox app → Palworld → Manage → Files → Browse)
2. **Install the Overkit probe**: from the [latest release](../../releases), copy the `PalworldMod/OverkitProbe` folder into `...\ue4ss\Mods\`.
3. **Run the overlay**: extract the `Overkit` folder anywhere and run `Overkit.Host.exe`. It sits in the tray and waits for the game.
4. Launch Palworld (borderless windowed), load a save — the HUD appears top-left, **F6** opens the panel.

> ℹ️ Open the palbox once per game session to let the game materialize every page — Overkit shows an honest `X/Y synced` counter until then.

## Known limitations (alpha)

- Dataset names are currently extracted from a **French** game install; other languages will come with the dataset pipeline
- Chest contents are not detected yet (bag, key items and food are)
- The map background is a stylized grid — the official map image will be extracted locally by the future installer (game assets are never redistributed)
- Palbox completeness depends on the game's lazy page sync (see the ℹ️ above)
- Steam build untested; multiplayer client mode untested (server-side data is then partially unavailable by design)
- Performance: no measurable FPS impact observed (~700 fps unchanged on the dev machine), formal P95/P99 measurement pending

## Building from source

- **Overlay (host)**: .NET 8 SDK — `dotnet build host/Overkit.Host -c Release`
- **Probe / Dumper (UE4SS C++ mods)**: Visual Studio 2026 (C++ workload), CMake ≥ 3.22, Rust ≥ 1.73, and a GitHub account linked to Epic Games (the RE-UE4SS `UEPseudo` submodule is gated by the Epic EULA). See `probe/README.md`
- **Dataset**: `dotnet run --project dataset/builder -- <raw_dir> <out_dir>` from the Dumper's raw table dumps

## Credits & legal

- Built on [RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) and the [Okaetsu Palworld fork](https://github.com/Okaetsu/RE-UE4SS) (MIT)
- Overkit is MIT-licensed (see [LICENSE](LICENSE))
- Dataset files are transformed data derived from Palworld, © Pocketpair — distributed the same way community wikis and calculators do, and removable on request. Game assets (icons, map image) are never redistributed
- Overkit is a fan project, not affiliated with Pocketpair

Development status and roadmap: [docs/etat-avancement.md](docs/etat-avancement.md) (French).
