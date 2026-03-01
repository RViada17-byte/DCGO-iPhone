# iOS Offline Boot (Track B)

This mode is for a playable local duel path on iPhone while bypassing online/menu-heavy startup.

## What it does
- Builds iOS with `DCGO_OFFLINE_BOOT=1`.
- Boots directly into `BattleScene`.
- Forces offline local mode (`GameMode.OfflineLocal`).
- Starts as AI/Bot duel flow (`isAI=true`).
- Uses fixed offline duel selectors by default in offline boot:
  - player: `ST1 Demo`
  - opponent: `ST2 Demo`
- Auto-starts duel without deck-selection menu in offline boot.

## Build command
```bash
cd "/Users/rodrigo/Desktop/Desktop - Rodrigo’s Mac mini/DCGO-iPhone/DCGO"
./tools/unity_build_ios.sh "$(pwd)/Builds/iOS-Offline" device 1
```

For simulator export:
```bash
./tools/unity_build_ios.sh "$(pwd)/Builds/iOS-Simulator-Offline" simulator 1
```

## Card art sync (lightweight)
Use BT1/BT2 + ST1/ST2 art for fast iteration and correct image matching with offline demo decks:
```bash
cd "/Users/rodrigo/Desktop/Desktop - Rodrigo’s Mac mini/DCGO-iPhone/DCGO"
./tools/sync_card_art.sh
```

To import all available card art (larger app size), use:
```bash
./tools/sync_card_art.sh "/Users/rodrigo/Desktop/Desktop - Rodrigo’s Mac mini/Digimon Card Tag Force Project/data/external/dcgo/images" ALL
```

By default this imports from:
`../Digimon Card Tag Force Project/data/external/dcgo/images`
into:
`Assets/StreamingAssets/Textures/Card`
and converts `.webp` to `.jpg` for runtime compatibility.

## Notes
- This path keeps DCGO engine/card script runtime intact.
- It intentionally skips normal title/opening/lobby flow.
- No network matchmaking is required.

## NPC map integration (next step)
For map-driven duels, call this before loading `BattleScene`:
```csharp
BootstrapConfig.SetMode(GameMode.OfflineLocal);
BootstrapConfig.ConfigureOfflineDuel("PlayerDeckNameOrId", "NpcDeckNameOrId", autoStartDuel: true);
```

Deck selectors accept exact deck name, deck ID, or partial deck name.

## Current limitations
- UI is not yet touch-optimized end-to-end.
- Offline mode is local human vs bot flow only.
- Full startup stability still must be validated on physical iPhone build/sign/install.

## Simulator caveat (important)
- Current Unity simulator export uses an `x86_64` runtime library (`libiPhone-lib.dylib`).
- Modern iOS simulator images on Apple Silicon expect `arm64` apps for install/run.
- Result: simulator launch is not a reliable gate for this project right now.
- Use a physical iPhone build (`device` SDK) for true Track B validation.
