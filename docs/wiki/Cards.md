# Making Cards

A **Card** is your own panel tab. You build it by picking options from lists — no code, no JSON to write by hand.

---

## Build one in the game

Press **F6** → **＋ Créer une card**.

1. **Name your card** — e.g. *My breeders*
2. **Pick a block** — "I want to…"
   - **Count something** — a big number, e.g. how many females you own
   - **Show a list** — a table, e.g. every Pal above level 40
   - **Get alerted** — a warning when a condition is met
   - **Show a game value** — day, time, total pals, number of bases…
3. **Pick a source** — your Pals, your bases, base workers, Pals around you
4. **Add filters** (optional) — pick a field, an operator and a value, then **+**. Filters stack: *level ≥ 40* **and** *gender = female*
5. **Add the block to your card** — the **live preview** on the right updates with your real data
6. Repeat for as many blocks as you want, then **Save**

Your card appears as a tab immediately. No restart.

## Edit or delete

The **Card en cours** dropdown at the top lets you reopen any card: its name and blocks come back, you adjust, you save. Renaming works too — the tab follows.

The **Supprimer** button removes your card and its file (with a confirmation).

> Cards shipped with Overkit are marked *(fournie)*. You can open one to see how it's built; saving it creates **your own copy**, which takes precedence over the original. They can't be deleted — they'd come back on the next update.

## Where cards live — and why it matters

| Location | Content | On an Overkit update |
|---|---|---|
| `%LOCALAPPDATA%\Overkit\Cards` | Cards you made | **Never touched** |
| `<install folder>\Cards` | Cards shipped with Overkit | Replaced |

Your creations live outside the install folder, so updating Overkit can never delete them. **Sharing a card with a friend is just sending them the JSON file** — they drop it in that folder and it appears in their panel.

## Under the hood: the expression language

The editor writes expressions for you, but you can open a card's JSON and edit it directly. The language is deliberately limited: no loops, no file or network access, and a time budget per refresh — a card can never slow your game down. If one misbehaves, it's suspended with a message explaining which block failed.

**Paths** — the game state, in lowercase with dots:

```
palbox.pals        world.time.hour      player.position.x
bases.list         nearby.actors        palbox.owned_count
```

**Filters and aggregates**, chained with `|`:

```
count(palbox.pals | where(gender = "female"))
palbox.pals | where(level >= 40)
palbox.pals | avg(talents.hp)
```

**Helpers**: `round`, `floor`, `abs`, `percent(a, b)`, `pad(value, length)`, `lower`, `contains`, `concat`, `if(condition, then, else)`, `isset`.

Full reference: [`docs/cards.md` in the repository](https://github.com/Overkit/overkit).

## Ideas to steal

- Count your females of a species you're breeding
- List Pals with a specific passive skill
- Alert when any base worker drops below 30 % sanity
- Show the in-game clock as `hh:mm` next to your pal count
