# ADR-0006 — Packaging des releases : self-contained maintenant, framework-dependent à la prochaine version majeure

- **Date** : 2026-08-03
- **Statut** : accepté
- **Contexte** : la release `v0.1.0-alpha` est publiée en **self-contained win-x64** (runtime .NET et Windows App SDK embarqués, ~118 Mo compressés) pour tenir P4 : « télécharger, double-cliquer, jouer », sans aucun prérequis.
  L'auto-modération de Nexus Mods a signalé l'archive. Analyse : **0/57 antivirus** sur VirusTotal ; le déclencheur est une règle comportementale Sigma (« Potential Vcruntime140 DLL Sideloading », Nextron Systems), qui se déclenche parce qu'un exécutable non signé charge `vcruntime140_cor3.dll` depuis son propre dossier — comportement normal de toute application .NET portable, la DLL étant signée Microsoft. Aucun malware, mais un motif que les heuristiques surveillent.
- **Décision** :
  1. La distribution reste **self-contained** pour les versions alpha en cours.
  2. Le passage en **framework-dependent** (paquet ~2 Mo, prérequis : .NET 8 Desktop Runtime côté joueur) est planifié pour la **prochaine version majeure publiée** — le changement d'instructions d'installation sera alors annoncé en même temps que les nouveautés, plutôt qu'en correctif isolé.
  3. La signature de code (certificat payant) reste à décider ; c'est le seul remède de fond au problème de réputation d'un exécutable indépendant.
- **Conséquences** : `release/package.ps1` devra proposer les deux modes (`-SelfContained`). Les README (EN/FR) et la page Nexus devront être mis à jour au moment du basculement, avec le lien vers le runtime Microsoft.

## Application (2026-08-03, v0.2.0-alpha)

Le basculement est fait. Configuration retenue : **.NET framework-dependent + Windows App SDK embarqué** — un seul prérequis pour le joueur (le .NET 8 Desktop Runtime), et `vcruntime140_cor3.dll` disparaît du paquet, supprimant la cause du signalement. Taille : 52 Mo contre 118.

Deux pièges rencontrés, documentés pour les prochaines releases :

1. `dotnet publish` d'une application WinUI non packagée **perd `Overkit.Host.pri` et les vues compilées `.xbf`** : le panneau plante au démarrage avec `XamlParseException`. Le paquet est donc assemblé depuis la sortie de `dotnet build -r win-x64 -o <dossier>`, et `package.ps1` échoue explicitement si le `.pri` manque.
2. Le crash était silencieux — aucune trace dans le journal ni dans l'observateur d'événements. Le host journalise désormais toute exception non gérée (`AppDomain`, `TaskScheduler`, `Application.UnhandledException`, thread du HUD).

## Distribution scindée (2026-08-03)

Nexus Mods bloque toute publication dès une détection VirusTotal, et la v0.2.0-alpha en récolte une : `Trojan.GenKryptik.Win64` chez **Zillya seul** (~70 autres moteurs propres). « GenKryptik » est une heuristique générique visant les binaires packés, que déclenche couramment l'*apphost* .NET d'une application non signée. Aucune variante de packaging ne l'élimine : l'apphost subsiste même en tout framework-dependent (mesuré : 37,7 Mo, `Overkit.Host.exe` toujours présent).

**Décision** : scinder la distribution.

- **Nexus Mods** héberge `OverkitProbe-<version>.zip` (213 Ko) : la sonde UE4SS, `mapping.json`, `enabled.txt`. Aucun exécutable, aucun binaire .NET. C'est le composant qui est réellement un mod de jeu.
- **GitHub Releases** héberge le paquet complet (overlay + sonde) et reste la source unique de l'application compagnon.

`package.ps1` produit désormais les deux archives. Une contestation de faux positif a été envoyée à Zillya ; la signature de code reste le seul remède de fond et demeure une décision ouverte.
