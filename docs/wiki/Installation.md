# Installation

Takes about five minutes. Two prerequisites, then three copy-paste steps.

---

## Requirements

- **Windows 10/11 x64**
- **[.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0/runtime)** — pick *Run desktop apps*. One click, once.
- **[UE4SS — Okaetsu experimental-palworld](https://github.com/Okaetsu/RE-UE4SS/releases/tag/experimental-palworld)** — the mod loader Overkit's probe runs on
- **Palworld in borderless windowed mode** — exclusive fullscreen hides every overlay, including this one

Tested on the **Game Pass (WinGDK)** build. Steam should work — paths differ, see below — but nobody has confirmed it yet. [Tell us if you try!](https://github.com/Overkit/overkit/issues)

## 1. Install UE4SS (skip if you already have it)

Download `UE4SS-Palworld.zip` from the link above, and extract **`dwmapi.dll`** and the **`ue4ss`** folder into your game's binaries folder:

| Version | Folder |
|---|---|
| Steam | `Palworld\Pal\Binaries\Win64\` |
| Game Pass | `Palworld\Content\Pal\Binaries\WinGDK\` |

> Finding the Game Pass folder: Xbox app → Palworld → **⋯ Manage** → **Files** → **Browse**.

> ⚠️ If you use the Steam Workshop version of UE4SS, do **not** install this one as well — two copies crash the game.

## 2. Install the Overkit probe

From the [latest release](https://github.com/Overkit/overkit/releases), unzip the package and copy the folder:

```
PalworldMod\OverkitProbe   →   ...\ue4ss\Mods\OverkitProbe
```

That folder contains the probe DLL, its `mapping.json` and an `enabled.txt`. Nothing else to configure.

## 3. Run the overlay

Extract the `Overkit` folder anywhere you like (Documents, a games folder — not inside the game's install) and run **`Overkit.Host.exe`**.

It parks itself in the system tray next to the clock and waits for Palworld.

## 4. Play

Launch Palworld in **borderless windowed**, load a save:

- the **HUD** appears top-left
- **F6** opens the interactive panel and frees your mouse cursor
- **F6** again (or the ✕) returns you to the game

> ℹ️ Open your palbox once per session so the game syncs every page — Overkit shows an honest `X/Y synced` counter until you do.

## Optional: a one-click launcher

Create a `.bat` file next to your game shortcut:

```bat
@echo off
start "" "C:\path\to\Overkit\Overkit.Host.exe"
start "" "steam://rungameid/1623730"
```

(Game Pass users: replace the second line with `start "" shell:AppsFolder\PocketpairInc.Palworld_ad4psfrxyesvt!AppPalShipping`.)

An installer that handles all of this is planned.

## Uninstalling

- Delete the `Overkit` folder
- Delete `...\ue4ss\Mods\OverkitProbe`
- Optionally delete `%LOCALAPPDATA%\Overkit` (your cards and settings)

Overkit never modifies the game's files or your saves, so nothing else is left behind.
