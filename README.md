# overkit-core

Source code of **Overkit — All-in-One Overlay for Palworld**: a free, read-only overlay that brings your palbox, base alerts, craft checklists, breeding pairs and a live farm map inside the game. MIT licensed.

- 🎮 **Players**: downloads and installation → **[Overkit/overkit](https://github.com/Overkit/overkit)** · **[Wiki](https://github.com/Overkit/overkit/wiki)**
- 📐 **Specification**: [docs/specification.md](docs/specification.md) — principles (P1…P7), architecture, State Bus, requirements (EXG-xxx) referenced throughout the code
- 📋 **Progress and roadmap**: [docs/etat-avancement.md](docs/etat-avancement.md) (French)
- 🏛 **Architecture decisions**: [docs/decisions/](docs/decisions/)
- 🃏 **Card language reference**: [docs/cards.md](docs/cards.md)

## Repository layout

```
overkit-core/
├── probe/            # The probe — a read-only UE4SS C++ mod (see probe/README.md to build)
├── host/             # .NET 8 overlay: Core (state bus, modules), Hud (WinForms), Host (WinUI 3)
├── sdk/              # Overkit.Sdk — the public contract for third-party modules
├── modules/          # Modules shipped with Overkit
├── cards/            # Cards shipped with Overkit
├── dumper/           # UE4SS mod that extracts the game's DataTables (dev tool)
├── dataset/          # Dataset builder + map calibration
├── schema/           # State Bus JSON Schema + C# type generator
├── release/          # Packaging script + binary license
├── scripts/          # Development helpers
└── docs/             # Progress, ADRs, wiki sources
```

## Design principles

- **Read-only, always.** The probe observes the game through Unreal reflection and has no write path — no save edits, no gameplay calls. This is what guarantees no corruption, no conflict with gameplay mods, and no cheating vector.
- **One component inside the game.** Only the probe runs in Palworld's process; third-party modules live in the overlay, out of process.
- **Graceful degradation.** Without the probe, the overlay runs in static mode with the dataset — a first-class mode, not a broken state.
- **Data-driven.** Game data lives in a versioned dataset and reflection paths in `mapping.json`; a game patch means regenerating data, not recompiling.

## Building

| Part | Requirements | Command |
|---|---|---|
| Overlay | .NET 8 SDK | `dotnet build host/Overkit.Host -c Release` |
| Probe / Dumper | VS 2026 (C++), CMake ≥ 3.22, Rust ≥ 1.73, GitHub account linked to Epic Games | see [probe/README.md](probe/README.md) |
| Dataset | .NET 8 SDK | `dotnet run --project dataset/builder -- <raw_dir> <out_dir>` |
| Release packages | above | `.\release\package.ps1 -Version <x.y.z>` |

`scripts/dev-restart.ps1` rebuilds, redeploys cards and modules, and restarts the overlay in one command.

## Contributing

Issues and pull requests are welcome. Two things to know:

- The probe is deliberately minimal and stays read-only — features belong in the overlay or in modules.
- The State Bus schema (`schema/state-bus.v1.schema.json`) is the single source of truth: C# types are generated from it, never edited by hand.

## Credits

Built on [RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) and the [Okaetsu Palworld fork](https://github.com/Okaetsu/RE-UE4SS). Overkit is a fan project, not affiliated with Pocketpair.
