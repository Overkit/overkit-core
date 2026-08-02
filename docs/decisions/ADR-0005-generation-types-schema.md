# ADR-0005 — Génération des types C# depuis le JSON Schema du State Bus

- **Date** : 2026-08-02
- **Statut** : accepté
- **Contexte** : EXG-020 impose une source unique (JSON Schema dans `schema/`) produisant trois artefacts : les types C# (host + SDK), la documentation, et la validation des messages de la Sonde. Il faut un outil de génération C# fiable et intégrable au build.
- **Options considérées** :
  1. **NJsonSchema.CodeGeneration.CSharp** (NuGet, écosystème .NET pur) — génération de records C# depuis le schéma, validation runtime via le même package ; s'intègre dans un petit générateur console maison lancé par `dotnet run`.
  2. quicktype (CLI Node) — bonne qualité de sortie mais introduit une dépendance Node.js dans la chaîne de build, étrangère au reste de la stack.
  3. Écriture manuelle des types + schéma comme simple doc — viole EXG-020 (dérive garantie entre schéma et code).
- **Décision** : option 1. Un projet console `schema/generator/` (NJsonSchema) génère `host/Overkit.Contracts/StateBus.g.cs` (types immuables). La CI vérifiera que le fichier généré est à jour (regénération + diff vide) pour empêcher toute dérive.
- **Conséquences** : première dépendance NuGet du projet, confinée à l'outillage (le générateur n'est pas livré aux joueurs). La Sonde C++ n'utilise pas de génération pour l'instant : elle émet un sous-ensemble du schéma, validé côté host en debug — la validation C++ compile-time sera réévaluée quand les collecteurs se multiplieront.
