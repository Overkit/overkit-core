# overkit-core

Socle privé d'Overkit — All-in-One Overlay for Palworld. Sources fermées pendant l'alpha (voir [LICENSE](LICENSE)).

- **Distribution publique** (releases binaires, doc utilisateur EN/FR) : [github.com/Overkit/overkit](https://github.com/Overkit/overkit)
- **État d'avancement et feuille de route** : [docs/etat-avancement.md](docs/etat-avancement.md)
- **Décisions d'architecture** : [docs/decisions/](docs/decisions/)
- **Packaging d'une release** : `.\release\package.ps1 -Version <x.y.z-alpha>` (publie le host, assemble binaires + dataset + mod + licence EULA)

```
overkit-core/
├── probe/            # Sonde — mod UE4SS C++ lecture seule (voir probe/README.md pour le build)
├── host/             # Overlay .NET 8 : Core (State Bus, modules), Hud (WinForms), Host (WinUI 3)
├── dumper/           # Mod UE4SS d'extraction des DataTables (outil de dev)
├── dataset/          # Builder du dataset + calibration carte
├── schema/           # JSON Schema du State Bus + générateur de types C#
├── release/          # Script de packaging + licence des binaires
└── docs/             # Avancement, ADR
```
