# Card Script Sync Workflow

This project keeps runtime card effects in:

- `Assets/Scripts/CardEffect`

Reference updates come from the sibling repository:

- `../DCGO-Card-Scripts/CardEffect`

You can override that path with `DCGO_CARD_SCRIPTS_DIR` or by passing an explicit path argument.

## Sync

```bash
./tools/sync_card_scripts.sh
```

Optional custom source path:

```bash
./tools/sync_card_scripts.sh "/absolute/path/to/DCGO-Card-Scripts/CardEffect"
```

Notes:

- Sync copies only `*.cs` files.
- Unity `.meta` files remain managed in this project.
- Sync uses `rsync --delete` to remove stale effect scripts from target.

## Consistency Check

```bash
./tools/check_card_script_consistency.sh
```

Checks performed:

- per-set `*.cs` count comparison between source and target
- each target file has a matching class declaration (`BT1_001.cs` -> `class BT1_001`)
- duplicate class names across target scripts
- known `CardEffectClassName = "..."` references in `Assets/Scripts` resolve to a class in card scripts

## Recommended Update Sequence

1. `./tools/sync_card_scripts.sh`
2. `./tools/check_card_script_consistency.sh`
3. `./tools/unity_check_compile.sh`
4. run local play smoke test in editor
