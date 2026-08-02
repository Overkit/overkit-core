# Probe — la Sonde Overkit

Mod UE4SS C++ en lecture seule (P1) : lit l'état de Palworld par réflexion et le publie sur un WebSocket local (`127.0.0.1:47800`). Seul composant du projet à s'exécuter dans le process du jeu (P2).

## Contenu

- `OverkitProbe/` — sources du mod (spike Phase 0 : chargement + cycle de vie ; le transport et les collecteurs arrivent ensuite)
- `spike-lua/` — spike jetable de validation de la lecture par réflexion en Lua (Phase 0)

## Prérequis de build

- Visual Studio 2026 avec la charge « Développement Desktop en C++ » (MSVC ≥ 14.43, CMake ≥ 3.22 inclus)
- Toolchain Rust ≥ 1.73 (dépendance patternsleuth de UE4SS)
- Compte GitHub lié à un compte Epic Games (accès sources Unreal) : le sous-module `deps/first/Unreal` (UEPseudo) de RE-UE4SS est privé, gated par l'EULA Epic — voir `docs/decisions/ADR-0003`

## Mise en place du workspace (hors monorepo)

Le mod se compile contre les sources de RE-UE4SS (fork Okaetsu), dans un workspace séparé pour garder le monorepo léger :

```
probe-workspace/
├── CMakeLists.txt      # add_subdirectory(RE-UE4SS) + add_subdirectory(OverkitProbe)
├── RE-UE4SS/           # clone du fork, checkout au SHA du UE4SS.dll déployé
└── OverkitProbe/       # jonction NTFS vers overkit/probe/OverkitProbe
```

```powershell
git clone https://github.com/Okaetsu/RE-UE4SS.git probe-workspace/RE-UE4SS
cd probe-workspace/RE-UE4SS
git checkout <SHA du build installé>   # visible dans UE4SS.log du jeu
git submodule update --init --recursive
cd ..
New-Item -ItemType Junction -Path OverkitProbe -Target ..\overkit\probe\OverkitProbe
```

SHA actuellement utilisé : `c838a8ac` (release `experimental-palworld`, assets du 2026-07-19). La règle : le mod doit être compilé au même commit que le `UE4SS.dll` chargé par le jeu (compatibilité ABI).

## Build

```powershell
cd probe-workspace
cmake -B build -G "Visual Studio 18 2026"
cmake --build build --config Game__Shipping__Win64 --target OverkitProbe
```

## Déploiement

Copier la DLL produite vers le dossier de mods UE4SS du jeu :

```
<jeu>\Pal\Binaries\<Win64|WinGDK>\ue4ss\Mods\OverkitProbe\dlls\main.dll
```

puis créer `Mods\OverkitProbe\enabled.txt` (vide) pour activer le mod. Redémarrer le jeu.
