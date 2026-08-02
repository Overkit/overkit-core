# ADR-0002 — Machine de dev : Palworld version Game Pass (WinGDK)

- **Date** : 2026-08-01
- **Statut** : accepté — **verdict Phase 0 : UE4SS fonctionne sur Game Pass/WinGDK** (à réévaluer avant la Phase 4 — installeur)
- **Contexte** : le cahier des charges (§7, installeur Velopack « détection Steam ») suppose la version Steam. La machine de dev principale possède la version **PC Game Pass** : binaire `Palworld-WinGDK-Shipping.exe` dans `G:\XboxGames\Palworld\Content\Pal\Binaries\WinGDK\`.
- **Faits** :
  - Le dossier `XboxGames\Palworld\Content` est lisible et navigable (contrairement à `WindowsApps`).
  - UE4SS sur les versions WinGDK est moins balisé que sur Steam (injection par DLL proxy à valider, ACL du dossier à vérifier au moment du dépôt des fichiers de la Sonde).
- **Décision** :
  1. Le spike Sonde (Phase 0) sera tenté sur la version Game Pass ; la faisabilité UE4SS/WinGDK devient une **inconnue supplémentaire de la Phase 0** à dérisquer.
  2. L'installeur (Phase 4) devra détecter **Steam ET Game Pass** — mise à jour du périmètre de P4.
  3. Si UE4SS s'avère non viable sur WinGDK, options par ordre de préférence : (a) dev sur une copie Steam, (b) mode statique seul pour Game Pass (P3), documenté.
- **Conséquences** : aucun impact sur le spike HUD. Le rapport de Phase 0 devra inclure le verdict UE4SS-sur-Game-Pass.
- **Verdict (2026-08-01)** : injection confirmée sur Palworld Game Pass 1.10.1103.0 avec RE-UE4SS Okaetsu v3.0.1 Beta (release `experimental-palworld`, assets du 2026-07-19), déposé dans `Pal\Binaries\WinGDK\` (`dwmapi.dll` + `ue4ss\`). Log propre : mods Lua chargés, boucle d'événements active, jeu stable. Particularité WinGDK : UE4SS voit les fichiers via le chemin virtualisé `C:\Program Files\WindowsApps\PocketpairInc.Palworld_…`, alors qu'ils sont physiquement dans `G:\XboxGames\Palworld\Content\…` — les deux chemins pointent vers les mêmes fichiers.
