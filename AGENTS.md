# Repository Instructions

## Device Build Policy

- When building for a physical iPhone or iPad, always use `ReleaseForRunning` unless the user explicitly asks for a `Debug` build.
- Do not install `Debug-iphoneos` builds onto the user's device for normal testing, performance checks, or QA signoff.
- Treat any device install from `Debug`, `Debug-iphoneos`, or equivalent debug configurations as incorrect unless the user specifically requests a debug/dev build for troubleshooting.
- Before reporting that a build is on the phone, confirm the installed app came from a `ReleaseForRunning-iphoneos` output path.

## Build Hygiene

- Every time we put together a new build, clean out old build outputs, logs, and other generated compile artifacts that are no longer needed.
- Remove stale contents from `Builds/`, `Logs/`, `Temp/`, and any other disposable output locations before or as part of the new build process.
- Delete unneeded generated files such as old Unity batch logs, temporary compile outputs, and other safe-to-regenerate artifacts so only the current build artifacts remain.
