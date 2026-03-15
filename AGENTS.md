# Repository Instructions

## Build Hygiene

- Every time we put together a new build, clean out old build outputs, logs, and other generated compile artifacts that are no longer needed.
- Remove stale contents from `Builds/`, `Logs/`, `Temp/`, and any other disposable output locations before or as part of the new build process.
- Delete unneeded generated files such as old Unity batch logs, temporary compile outputs, and other safe-to-regenerate artifacts so only the current build artifacts remain.
