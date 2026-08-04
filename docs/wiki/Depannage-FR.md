# Dépannage

Commence ici quand quelque chose ne s'affiche pas. Presque tout se lit dans le journal : **icône de la zone de notification → Ouvrir le journal** (`overkit.log`, à côté de l'exécutable).

---

## Le HUD n'apparaît pas du tout

1. Palworld est-il en **fenêtré sans bordure** ? Le plein écran exclusif masque tous les overlays.
2. Palworld est-il la **fenêtre active** ? Le HUD se masque volontairement quand tu alt-tab.
3. `Overkit.Host.exe` tourne-t-il ? Cherche son icône près de l'horloge (peut-être dans le tiroir `^` des icônes masquées).
4. Runtime manquant ? Si l'application se ferme instantanément, installe le [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime).

## Le HUD affiche « ○ Overkit — hors ligne »

L'overlay tourne mais la sonde ne répond pas. La sonde vit dans le jeu, donc :

- As-tu **relancé Palworld** après avoir installé la sonde ? Elle ne se charge qu'au démarrage du jeu.
- Vérifie que `...\ue4ss\Mods\OverkitProbe\` contient `dlls\main.dll` et `enabled.txt`.
- Vérifie qu'UE4SS s'est chargé : `...\ue4ss\UE4SS.log` doit mentionner `OverkitProbe`.
- Un seul programme à la fois peut dialoguer avec la sonde.

## La palbox affiche « 43/64 synchronisés »

C'est normal. Le jeu ne matérialise une page de Palbox qu'une fois affichée. **Ouvre ta boîte en jeu et fais défiler les onglets** — Overkit se complète dans les 30 secondes. Il affiche le compte honnête plutôt que de faire croire que ta boîte est complète.

## L'audit de base affiche « Pal 7379676a » au lieu des noms

L'audit a besoin de la palbox pour retrouver les noms. Ouvre ta boîte une fois (voir ci-dessus) et les vrais noms apparaissent.

## Une card affiche « Card suspendue »

Le message indique le bloc fautif. Causes fréquentes :

- un filtre qui compare du texte à un nombre, ou l'inverse
- une source vide sur le moment (aucune base, aucun Pal à proximité)

Corrige le bloc dans l'éditeur, enregistre : la card réessaie automatiquement. Après trois échecs consécutifs, elle reste suspendue jusqu'à correction.

## Les points de la carte semblent mal placés

La carte est une grille stylisée, pas l'image du jeu, et sa calibration a été mesurée sur une seule sauvegarde. Si ta position semble décalée par rapport à la carte in-game, [ouvre une issue](https://github.com/Overkit/overkit/issues) avec une capture — recalibrer est rapide.

## Les coffres ne sont pas comptés dans la checklist de craft

Limitation connue : le sac, les objets clés et la boîte à nourriture sont lus ; le contenu des coffres n'est pas encore détecté.

## L'antivirus signale le téléchargement

Le paquet ne contient aucun malware (VirusTotal : 0/57). Un exécutable indépendant non signé n'a pas de score de réputation, ce que certaines heuristiques n'aiment pas. Depuis la v0.2.0-alpha, le paquet n'embarque plus le runtime .NET, ce qui a supprimé le motif précis que signalaient les scanners automatiques.

## Rien de tout ça n'a aidé

[Ouvre une issue](https://github.com/Overkit/overkit/issues) avec :

- ce que tu attendais et ce qui s'est passé
- ton `overkit.log`
- ta version du jeu (Steam ou Game Pass) et la version d'Overkit

Le journal enregistre désormais aussi les pannes non gérées : il suffit généralement à cerner le problème.
