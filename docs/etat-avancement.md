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

**Phase 3 — API, SDK et Cards** (en cours depuis le 03/08/2026)

- [x] `Overkit.Sdk` : `IOverkitModule`, manifeste, `IRefData`, snapshot immuable, vues déclaratives (ADR-0007)
- [x] Chargeur de modules : `AssemblyLoadContext` collectible, compatibilité vérifiée avec raison affichée (EXG-070), panne d'un module isolée (EXG-060)
- [x] **Critère de sortie partiel atteint** : un module compilé hors du host, déposé dans `Modules/`, se charge et s'affiche (`Overkit.Module.BaseAudit`)
- [x] Moteur de Cards : interpréteur borné, sections déclaratives, un fichier JSON par Card (EXG-040, EXG-041) ; module Alertes livré en Card (§6.5) ; guide dans `docs/cards.md`
- [x] Éditeur de Cards in-game : blocs guidés (compter / lister / alerter / afficher), filtres champ-opérateur-valeur, aperçu en direct, création-modification-suppression à chaud ; cards du joueur stockées hors installation (survivent aux mises à jour)
- [ ] Template `dotnet new overkit-module` + package NuGet du SDK
- [ ] Sections interactives (saisie, sélection) — le déclaratif v1 ne couvre que l'affichage, d'où les vues Palbox/Craft/Carte restées intégrées

**Écosystème — dépôts de registre** (prévu)

Trois dépôts distincts recenseront les add-ons communautaires, publication par pull request (EXG-081) : modules C#, scripts Lua, et Cards JSON. Un manifeste par add-on, validation du schéma et de la licence en CI, merge = apparition au catalogue.

**Phase 2 — Modules fondateurs** (en cours depuis le 02/08/2026)

- [x] Audit de base : alertes faim/santé mentale des travailleurs (validé sur données réelles)
- [x] Checklist de craft : recette × quantité → manquants → espèces à farmer (drops)
- [x] Accouplement inversé : CombiRank + combos uniques, paires réalisables avec la Palbox réelle (genres inclus)
- [x] Vue carte (fond stylisé v1, bases, position live, spots jour/nuit) + Routing de farm (clustering, distances, cible HUD)
- Critère de sortie : les 3 modules utilisés en session réelle une semaine, sans alt-tab

**Phase 1 — Noyau** (bouclée le 02/08/2026)

- [x] Schéma State Bus v1 (JSON Schema → génération types C#, EXG-020)
- [x] Sonde (v0.6.0) : les 8 domaines du State Bus collectés (`player`, `world`, `palbox`, `party`, `bases`, `inventory`, `nearby`, `collectors`), `mapping.json` externalisé rechargeable à chaud (EXG-003 démontrée : sabotage/réparation en live sans recompilation)
- [x] **Critère de sortie atteint** : la Palbox s'affiche dans le panneau en live (WinUI 3, recherche/tri, noms localisés via le dataset, équipe marquée ★, curseur libéré)
- [x] Dataset : Dumper générique piloté à chaud + builder complet — `pals`, `passives`, `breeding`, `items`, `recipes`, `drops`, `spawners` (+ `mapping.json`, calibration). Première release publiée : `dataset-1.10.1103.0-r1`
- [x] Host : State Bus, connexion Sonde typée + reconnexion, mode statique, HUD compact lié au jeu, panneau WinUI 3, refdata, calibration, settings, hotkey, tray
- Critère de sortie : EXG-003, 010, 011 verts ; EXG-013 validé au niveau spike (mesure fine P95/P99 à refaire) ; EXG-030 démontré (pipeline en minutes) ; la Palbox s'affiche dans le panneau en live ✓

**Phase 0 — Spike de faisabilité** (bouclée le 02/08/2026)

- [x] Spike HUD : fenêtre click-through + hotkey panneau (`host/spikes/HudSpike`) — validé le 01/08/2026
- [x] Spike Sonde : position joueur par réflexion → WebSocket local (127.0.0.1:47800), consommé en live par le HUD — validé le 02/08/2026 (version Game Pass/WinGDK)
- [x] Calibration carte : transformation affine monde→carte résolue sur 2 points, validée exacte sur un 3e (`dataset/map_calibration.draft.json`)
- [x] Mesure frametime avec/sans overlay : aucun impact mesurable (~700 FPS constants, compteur NVIDIA). Mesure fine P95/P99 à refaire en Phase 1 (PresentMon 2.5.1 erratique sur Windows 11 26200)
- [x] Heure in-game : `PalGameStateInGame.WorldTime.Ticks` (struct GameDateTime, répliquée) publiée à 1 Hz, affichée dans le HUD (`probe/mapping.draft.json`) — validé le 02/08/2026
- [~] Vidéo de démo : reportée volontairement — elle sera tournée sur une base complète et présentable aux utilisateurs finaux

## Licences

Code sous MIT (voir cahier des charges §10 pour le détail par artefact).
