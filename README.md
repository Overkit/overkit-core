# Overkit — All-in-One Overlay for Palworld

Overlay tout-en-un affiché par-dessus Palworld (aucun alt-tab), alimenté en temps réel par l'état du jeu, extensible par des modules communautaires, 100 % gratuit et open source.

> Document de référence : [cahier des charges v1.0](../cahier-des-charges-overkit.md)

## Principes non négociables

- **P1 — Lecture seule.** Aucun canal d'écriture vers le jeu, jamais.
- **P2 — Le tiers ne touche pas au process du jeu.** Seule la Sonde s'exécute dans Palworld.
- **P3 — Dégradation gracieuse.** Sans Sonde, mode statique de première classe.
- **P4 — Zéro friction d'installation.** Un installeur unique.
- **P5 — Pas d'alt-tab.** HUD passif + panneau sur hotkey.
- **P6 — Data-driven.** Dataset versionné par patch, rien en dur.
- **P7 — Tout est open source.**

## Structure du monorepo

```
overkit/
├── probe/            # mod UE4SS C++ (CMake) — la Sonde, lecture seule
├── host/             # app .NET 8 (host + HUD + panneau)
│   └── spikes/       # spikes de la Phase 0 (jetable, hors produit final)
├── sdk/              # Overkit.Sdk (NuGet) + template dotnet new
├── modules/          # modules internes v1
├── dumper/           # mod UE4SS de dump + console de post-traitement
├── schema/           # JSON Schema du State Bus + manifestes
├── dataset/          # scripts de build du dataset (pas les données)
├── infra/            # docker-compose Hub, config Traefik
├── docs/             # site docs + décisions (ADR)
└── .github/          # Actions CI/CD
```

## État d'avancement

**Phase 0 — Spike de faisabilité** (en cours)

- [x] Spike HUD : fenêtre click-through + hotkey panneau (`host/spikes/HudSpike`) — validé le 01/08/2026
- [ ] Spike Sonde : position joueur + heure in-game → WebSocket local
- [ ] Mesure frametime avec/sans overlay

## Licences

Code sous MIT (voir cahier des charges §10 pour le détail par artefact).
