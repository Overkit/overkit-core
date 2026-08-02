# ADR-0004 — Transport de la Sonde : serveur WebSocket minimal maison

- **Date** : 2026-08-02
- **Statut** : accepté (réévaluable si le besoin dépasse le cadre ci-dessous)
- **Contexte** : la Sonde publie l'état du jeu sur un WebSocket local (`127.0.0.1:47800`, EXG-002). Les bibliothèques C++ candidates (IXWebSocket, websocketpp/asio, uWebSockets) apportent des milliers de lignes de dépendances dans le seul composant qui s'exécute dans le process du jeu (P2) — surface d'audit et de risque disproportionnée pour le besoin réel : un serveur loopback, un client unique (le host), flux strictement descendant.
- **Décision** : implémentation maison d'un sous-ensemble de RFC 6455 (~300 lignes, `WsServer.cpp`) :
  - handshake HTTP avec `Sec-WebSocket-Accept` (SHA-1 via BCrypt, Base64 via Crypt32 — API Windows, zéro dépendance embarquée) ;
  - trames texte sortantes non masquées ; entrantes : seuls `ping` (→ `pong`) et `close` sont traités, le texte entrant est ignoré — le canal est descendant par construction, en cohérence avec P1 ;
  - modèle « dernier état » : les collecteurs publient un snapshot, le thread serveur pousse au client dès qu'un nouvel état existe ; pas de file, pas de backpressure à gérer ;
  - bind en dur sur `127.0.0.1`, aucune option pour binder ailleurs (EXG-002).
- **Limites assumées** : un seul client servi à la fois ; trames de contrôle fragmentées non gérées (jamais observées en loopback) ; pas de TLS (loopback). Si un besoin multi-client ou d'entrant structuré apparaît, réévaluer une bibliothèque éprouvée dans un ADR dédié.
- **Conséquences** : liens supplémentaires `ws2_32`, `bcrypt`, `crypt32` ; le host peut se connecter avec n'importe quel client WebSocket standard (`ClientWebSocket` .NET, navigateur, `websocat`) — debuggable au navigateur comme voulu au §7.
