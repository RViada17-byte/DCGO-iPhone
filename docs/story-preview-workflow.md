# Story Preview Workflow

This is the current working loop for Story Mode preview builds on macOS. It is the path that produced the working `DCGO-StoryPreview-Izzy` builds.

## Compile Check

Run:

```bash
/Users/rodrigo/Dev/DCGO/tools/unity_check_compile.sh
```

This validates that the project compiles in Unity batch mode before spending time on a full app build.

## Build A macOS Preview App

Run:

```bash
/Users/rodrigo/Dev/DCGO/tools/unity_build_macos.sh /Users/rodrigo/Dev/DCGO/Builds/Mac/DCGO-StoryPreview.app
```

What this script does:

1. Builds a fresh macOS Unity app bundle.
2. Clears macOS extended attributes from the bundle with `xattr -cr`.
3. Applies an ad-hoc signature with `codesign --force --deep --sign - --timestamp=none`.
4. Verifies the resulting bundle with `codesign --verify --deep --strict`.

This avoids the Finder-side "damaged or incomplete" failure we hit with unsigned or partially signed local Unity builds.

## Launch The Preview

Launch from Terminal:

```bash
open -n /Users/rodrigo/Dev/DCGO/Builds/Mac/DCGO-StoryPreview.app
```

Launching from Terminal is the most reliable local verification path for these preview builds.

## Logs

Build log:

```text
/Users/rodrigo/Dev/DCGO/Logs/macos-build.log
```

Runtime player log:

```text
~/Library/Logs/DefaultCompany/DCGO/Player.log
```

Startup trace:

```text
~/Library/Application Support/DefaultCompany/DCGO/startup-trace.log
```

The startup trace is useful for confirming scene flow such as:

- `Opening`
- `MainMenuRouter.OpenStory`
- `BattleScene` loaded additively

## Story Flow Verification

Current verified path:

1. Build app with `unity_build_macos.sh`
2. Launch app with `open -n`
3. Open Story Mode
4. Select Adventure -> Izzy
5. Story handoff reaches battle scene
6. `EnemyDeckData override applied -> Izzy` appears in the runtime log

## Known Notes

- `spctl --assess` may still reject the build because it is ad-hoc signed and not notarized. That is expected for local preview builds.
- The authoritative local check is `codesign --verify --deep --strict` plus a real terminal launch.
- There is existing project noise in the player log from missing legacy scene scripts. Those warnings are not the same thing as Story Mode regressions.
