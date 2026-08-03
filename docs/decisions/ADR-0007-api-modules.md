# ADR-0007 — API de modules : vues déclaratives plutôt qu'accès à l'UI

- **Date** : 2026-08-03
- **Statut** : accepté
- **Contexte** : la Phase 3 extrait l'API plugin de ce que partagent les modules fondateurs. Deux façons de laisser un module s'afficher :
  1. lui donner accès à l'arbre visuel (il crée ses propres contrôles WinUI) ;
  2. lui faire **décrire** ce qu'il veut afficher, le host se chargeant du rendu.
- **Décision** : option 2. Le SDK expose un modèle de vue déclaratif (`ModuleView` composée de `StatusSection`, `AlertsSection`, `TableSection`, `GaugesSection`, `CountersSection`, `EmptySection`). Un module ne référence aucune bibliothèque d'UI et ne crée aucune fenêtre — il remplit des slots, conformément au §5.3.
- **Raisons** :
  - **Isolation réelle** : un module ne peut pas casser le layout, geler l'UI ni atteindre le modèle interne du host (EXG-061) ; le SDK ne référence que `Overkit.Contracts`.
  - **Un seul moteur de rendu** pour les trois niveaux d'add-ons : les Cards (niveau 1) et les scripts Lua (niveau 2) produiront le même modèle que les modules C# (niveau 3).
  - **Portabilité de l'UI** : le jour où le rendu change (WinUI → autre chose), aucun module tiers n'est cassé.
  - **Testabilité** : un module est une fonction pure snapshot → vue, testable sans jeu ni interface.
- **Conséquences** :
  - Les vues riches et interactives (Palbox avec tri/recherche, carte avec canvas, autosuggestion) restent des vues intégrées au host : le modèle déclaratif v1 ne couvre pas l'interaction. Les sections interactives (champ de saisie, sélection, action) sont une extension à concevoir avant d'ouvrir l'écosystème.
  - Le chargement isole chaque module dans un `AssemblyLoadContext` collectible ; `Overkit.Sdk` et `Overkit.Contracts` sont **toujours fournis par le host** (un module qui embarquerait sa copie aurait des types incompatibles — verrouillé côté build et côté résolution).
  - Compatibilité vérifiée au chargement (schéma, domaines, capacités) : refus explicite avec raison affichée, jamais silencieux (EXG-070). Exception d'un module = désactivation signalée, host et autres modules indemnes (EXG-060).
