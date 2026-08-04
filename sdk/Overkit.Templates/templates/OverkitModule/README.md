# MODULE_DISPLAY_NAME

An [Overkit](https://github.com/Overkit/overkit) module — a panel tab fed by the live state of Palworld.

## Build

```bash
dotnet build -c Release
```

## Install

Copy the produced DLL into a subfolder of the overlay's `Modules/` directory:

```
Overkit\Modules\MODULE_SLUG\MyOverkitModule.dll
```

Restart the overlay: the module appears as a tab. Loading problems (missing
state domain, unsupported schema, exception) are reported in `overkit.log` and
on the tab itself — a failing module never takes the overlay down with it.

## What a module can and cannot do

- It receives an **immutable snapshot** of the game state and **describes** a
  view (status lines, counters, tables, gauges, alerts). The overlay renders it.
- It has **no write path to the game**: Overkit is read-only by design.
- It creates **no window**: the overlay owns the layout.
- It only gets what its manifest declares: state domains, and capabilities such
  as `refdata` for dataset access.

See the [SDK reference](https://github.com/Overkit/overkit-core/blob/main/docs/modules.md).
