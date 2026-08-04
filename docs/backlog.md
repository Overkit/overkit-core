# Backlog

Ce qui reste à faire, par ordre de dépendance. L'avancement par phase vit dans
[etat-avancement.md](etat-avancement.md) ; ce fichier détaille le reste à faire.

## À vérifier au prochain lancement du jeu

Rien ici ne bloque le développement, mais chaque point demande une session
Palworld pour être validé.

- [ ] Charger dans `Modules/` un module produit par `dotnet new overkit-module`
      et confirmer qu'il apparaît en onglet (le seul chemin non testé du
      template est le chargement à chaud, la compilation est vérifiée).
- [ ] Cards et modules après le passage des identifiants en `fr.overkit.*` :
      les réglages persistés sont indexés par identifiant, ils repartent à zéro.
- [ ] Sections interactives dans l'onglet Audit de base : le seuil, le filtre par
      base et la bascule « critiques seulement » doivent recalculer les alertes
      sans perdre le focus pendant qu'un snapshot arrive.
- [ ] Card `Recherche` : les quatre champs filtrent la liste, et les réglages
      sont retrouvés au lancement suivant (`%LOCALAPPDATA%\Overkit\card-inputs.json`).
- [ ] Onglets Palbox et Craft repassés en déclaratif : recherche, tri, choix de
      recette et quantité, avec la même lisibilité qu'avant la conversion —
      c'est le point à regarder de près, le rendu générique est plus sobre que
      les vues WinUI qu'il remplace.
- [ ] Éditeur in-game : créer une saisie par « Demander une valeur au joueur »,
      puis un filtre qui la vise via « Comparer à », et vérifier que l'aperçu
      réagit à la frappe sans perdre la valeur quand un bloc est ajouté.
- [ ] Coffres non détectés par le collecteur `bases` — la réflexion trouve les
      bases et les travailleurs, pas les conteneurs.
- [ ] Orientation de la carte : la transformation affine est exacte sur les
      points de calibration, l'accord visuel sur toute la surface reste à
      confirmer.
- [ ] Frametime P95/P99 (EXG-013) : validé au niveau spike seulement, PresentMon
      2.5.1 se comporte mal sur Windows 11 26200.

## Écosystème

- [ ] **Ne rien publier pour l'instant.** Les trois paquets
      (`Overkit.Contracts`, `Overkit.Sdk`, `Overkit.Templates`) se construisent
      dans `release/out/nuget/` et s'installent depuis une source locale ; la
      mise en ligne sur nuget.org attend une décision.
- [ ] Trois dépôts de registre — modules C#, scripts Lua, Cards JSON. Un
      manifeste par add-on, validation du schéma et de la licence en CI,
      publication par pull request, merge = apparition au catalogue (EXG-081).

## Phase 3 — reste

- [ ] Options de `choice` calculées par une expression (`options_from`) — pour
      proposer la liste des bases ou des espèces possédées plutôt qu'une liste
      figée.
- [ ] Boutons et lignes cliquables dans les Cards : sans code à exécuter, il
      faudrait leur donner un sens déclaratif (remettre les champs à zéro,
      basculer une valeur).
- [ ] Reprendre la vue Carte en déclaratif. Palbox et Craft sont passés
      modules ; la carte demande en plus une section de rendu graphique
      (fond, points, tracés) que le modèle de vue ne décrit pas.
- [ ] Accouplement reste une vue intégrée : elle a besoin d'une sélection de
      Pal parent à deux niveaux, que `TableSection` + `SelectionId` ne couvre
      qu'à moitié.

## Phase 4

- [ ] Scripting Lua (MoonSharp) pour les add-ons légers.
- [ ] Installeur unique (Velopack) — P4, zéro friction.
- [ ] Hub (§8) : catalogue en ligne des add-ons, sur les sous-domaines de
      `nallraen.fr`.

## Distribution

- [ ] Signature de code : reportée. Sans certificat, la sonde déclenche
      l'heuristique « Unsigned DLL Loaded by Windows Utility » sur VirusTotal,
      ce qui bloque la publication sur Nexus Mods. Les pistes gratuites pour
      projets open source (SignPath Foundation, Certum) restent ouvertes.
- [ ] Sonde seule sur Nexus Mods, overlay sur les releases GitHub — le partage
      en deux téléchargements contourne le blocage.
- [ ] Rapport de faux positif à envoyer à Zillya
      (`Trojan.GenKryptik.Win64.74308`, 0/57 chez les autres moteurs).

## Polish

- [ ] WebSocket de la sonde : un seul client à la fois, deux overlays ouverts se
      chassent l'un l'autre.
- [ ] Retours des testeurs (aucun défaut majeur signalé après 24 h d'usage à
      plusieurs, quelques pistes d'amélioration à trier).
- [ ] Vidéo de démonstration, à tourner sur une base présentable.
