# Troubleshooting

Start here when something doesn't show up. Almost everything is answered by the log: **tray icon → Open the log** (`overkit.log`, next to the executable).

---

## The HUD doesn't appear at all

1. Is Palworld in **borderless windowed**? Exclusive fullscreen hides every overlay.
2. Is Palworld the **focused window**? The HUD hides itself on purpose when you alt-tab.
3. Is `Overkit.Host.exe` running? Look for its icon near the clock (it may be in the hidden-icons drawer `^`).
4. Missing runtime? If the app closes instantly, install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime).

## The HUD says "○ Overkit — offline"

The overlay is running but the probe isn't answering. The probe lives in the game, so:

- Did you **restart Palworld** after installing the probe? It only loads at game start.
- Check `...\ue4ss\Mods\OverkitProbe\` contains `dlls\main.dll` and `enabled.txt`.
- Check UE4SS itself loaded: `...\ue4ss\UE4SS.log` should mention `OverkitProbe`.
- Only one program can talk to the probe at a time.

## The palbox shows "43/64 synced"

Normal. The game only materializes a palbox page once you've displayed it. **Open your palbox in game and scroll through the tabs** — Overkit fills in within 30 seconds. It shows the honest count rather than pretending your box is complete.

## Base audit shows "Pal 7379676a" instead of names

The audit needs the palbox to resolve names. Open your palbox once (see above) and the real names appear.

## A card says "Card suspendue"

The message names the block that failed. Common causes:

- a filter compares text to a number, or the other way round
- a source that's empty right now (no bases, no Pals nearby)

Fix the block in the editor, save, and the card retries automatically. If a card fails three times in a row, it stays suspended until you correct it.

## The map dots look misplaced

The map is a stylized grid, not the game's image, and its calibration was measured on one save. If your position looks off compared to the in-game map, [open an issue](https://github.com/Overkit/overkit/issues) with a screenshot — recalibrating is quick.

## Chests aren't counted in the craft checklist

Known limitation: bag, key items and food box are read; chest contents are not detected yet.

## Antivirus flags the download

The package contains no malware (VirusTotal: 0/57). Unsigned indie executables have no reputation score, which some heuristics dislike. Since v0.2.0-alpha the package no longer bundles the .NET runtime, which removed the specific pattern automated scanners were flagging.

## Nothing above helped

[Open an issue](https://github.com/Overkit/overkit/issues) with:

- what you expected and what happened
- your `overkit.log`
- your game version (Steam or Game Pass) and Overkit version

The log now records unhandled crashes too, so it's usually enough to pinpoint the problem.
