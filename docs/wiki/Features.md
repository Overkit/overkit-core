# Features

Everything Overkit gives you, tool by tool. Press **F6** in game to open the panel (rebindable); the HUD is always there, discreetly.

---

## HUD — the always-on pill

Top-left of your screen, click-through (your clicks go to the game). It shows:

- **● live / ○ offline** — whether the probe is connected
- **In-game day and time** — `J229 13:01`
- **Map coordinates** — the same numbers as the in-game map
- **Palbox counter** — `52 pals`, or `43/64 pals*` when the game hasn't synced every page yet
- **Party size** and, if you set one, your **farm target with live distance**

The HUD hides itself when Palworld isn't the focused window, so it never sits on top of your desktop or another app.

## Palbox

Every Pal you own — box, active party and base workers — in one searchable list.

- Localized species names, nickname, gender, level
- **IVs, labeled**: `PV / MÊL / TIR / DÉF` (HP, melee, shot, defense)
- Passive skills, with their real in-game names
- **Search** across names, species and passives; **sort** by level, name or total IVs
- Party members are marked with a ★

> ℹ️ The game only materializes a palbox page once you've opened it. Until then Overkit shows an honest `X/Y synced` counter rather than pretending your box is empty. Opening the box once per session fixes it.

## Base audit

Watches the wellbeing of every worker in every base and raises alerts before things go wrong:

- ⚠ **Warning** below 50 % hunger or sanity
- 🛑 **Critical** below 25 %

Each alert names the Pal, so you know who to feed or who needs a hot spring.

## Craft checklist

Pick a recipe and a quantity. Overkit compares it against **your whole inventory** (bag, key items, food box) and tells you:

- what you already have, per material
- **what's missing**, in red
- **which Pals drop the missing material** — straight from the game's drop tables

## Reverse breeding

Pick the Pal you want. Overkit lists **every parent pair that produces it**, using the game's official CombiRank formula plus the unique combos.

The killer feature: pairs you can **actually make with your own palbox** — genders included — are listed first. Toggle "Mes paires" off to see every theoretical pair.

> Results are possibilities, not certainties: passive-skill inheritance is probabilistic and isn't computed yet.

## Map & farm routing

A stylized map showing your bases (⌂), your **live position**, and the spawn spots of any species you search for:

- Green dots = spawns anytime · Purple = **night only**
- Dot size = how many spawners are clustered there
- The list on the right is **sorted by real distance** from you
- Hit 🎯 on a spot and it becomes your **HUD target**: close the panel and the distance ticks down as you run

The day/night filter uses the actual in-game clock, so a night-only spot won't be suggested at noon.

## Cards — build your own

The panel isn't limited to what ships with Overkit. The **＋ Créer une card** tab lets you build your own panels by picking blocks from lists — no code. See **[Making Cards](Cards)**.

## Tray icon

Next to the clock: open the panel, edit settings (`overkit.settings.json` — probe port, hotkey, game process names), open the log, or quit Overkit.
