# ADR-0002 — Machine de dev : Palworld version Game Pass (WinGDK)

- **Date** : 2026-08-01
- **Statut** : accepté (à réévaluer avant la Phase 4 — installeur)
- **Contexte** : le cahier des charges (§7, installeur Velopack « détection Steam ») suppose la version Steam. La machine de dev principale possède la version **PC Game Pass** : binaire `Palworld-WinGDK-Shipping.exe` dans `G:\XboxGames\Palworld\Content\Pal\Binaries\WinGDK\`.
- **Faits** :
  - Le dossier `XboxGames\Palworld\Content` est lisible et navigable (contrairement à `WindowsApps`).
  - UE4SS sur les versions WinGDK est moins balisé que sur Steam (injection par DLL proxy à valider, ACL du dossier à vérifier au moment du dépôt des fichiers de la Sonde).
- **Décision** :
  1. Le spike Sonde (Phase 0) sera tenté sur la version Game Pass ; la faisabilité UE4SS/WinGDK devient une **inconnue supplémentaire de la Phase 0** à dérisquer.
  2. L'installeur (Phase 4) devra détecter **Steam ET Game Pass** — mise à jour du périmètre de P4.
  3. Si UE4SS s'avère non viable sur WinGDK, options par ordre de préférence : (a) dev sur une copie Steam, (b) mode statique seul pour Game Pass (P3), documenté.
- **Conséquences** : aucun impact sur le spike HUD. Le rapport de Phase 0 devra inclure le verdict UE4SS-sur-Game-Pass.
