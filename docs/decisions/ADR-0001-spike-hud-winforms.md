# ADR-0001 — Spike HUD en WinForms sous `host/spikes/`

- **Date** : 2026-08-01
- **Statut** : accepté
- **Contexte** : la Phase 0 exige un spike « fenêtre HUD click-through + hotkey panneau ». Le cahier des charges (§7) prévoit pour le produit final une fenêtre HUD Win32/composition dédiée et un panneau WinUI 3. Un spike doit valider les mécanismes de fenêtrage au plus vite, pas préfigurer l'UI finale.
- **Décision** :
  1. Le spike vit dans `host/spikes/HudSpike` — code jetable, clairement séparé du produit.
  2. Il est écrit en **WinForms (.NET 8)** : c'est le chemin le plus court pour valider `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, `RegisterHotKey`, le re-`SetWindowPos` périodique et la restitution de focus. Ces mécanismes sont du Win32 pur (P/Invoke) et se transposent tels quels à la fenêtre HUD finale.
  3. Cible `net8.0-windows` (conforme au cahier des charges §7), compilé avec le SDK .NET 10 présent sur la machine de dev (runtime 8.0.27 présent).
- **Conséquences** : le code du spike ne sera pas réutilisé tel quel ; seuls les P/Invoke et la logique de bascule HUD↔panneau seront extraits vers la fenêtre HUD Win32 finale.
