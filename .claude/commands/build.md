---
description: Build the GetHub portable distribution
argument-hint: [-Run] [-Zip] [-Runtime <rid>]
---

Run the GetHub build script with any provided arguments.

Steps:
1. If `GetHub.exe` is currently running, tell the user to close it (the script will refuse otherwise — DLLs/exe are locked).
2. Execute `build.ps1` from the project root using PowerShell, forwarding `$ARGUMENTS` as-is.
   - Working directory: `C:\X-Files\Repos\~PERSONAL\GetHub\sourcegit`
   - Command: `powershell -ExecutionPolicy Bypass -File ".\build.ps1" $ARGUMENTS`
3. Report the final dist size and path from the `[done]` line.
4. If the user passed `-Run`, the script launches the exe automatically — don't launch it yourself.

Notes:
- Output goes to `GetHub_Dist/` (gitignored).
- The `data/` folder is automatically backed up to `%TEMP%`, the dist is wiped, then `data/` is restored — user prefs/repos persist across rebuilds.
- Current build flags (in build.ps1): trimmed + single-file + compressed, AOT disabled, update detection disabled.
- Common invocations:
  - `/build` — rebuild
  - `/build -Run` — rebuild and launch
  - `/build -Zip` — rebuild and produce versioned zip
  - `/build -Runtime win-arm64` — target ARM64
