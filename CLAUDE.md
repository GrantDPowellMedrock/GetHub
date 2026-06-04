# CLAUDE.md — GetHub

Context for working on this repo. Read this first.

## What this is
GetHub is a **private-origin, public fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit)** (MIT), rebranded `SourceGit`→`GetHub`. A free, fast Avalonia Git GUI. Owner: Grant Powell (`GrantDPowellMedrock`).

- **Repo:** https://github.com/GrantDPowellMedrock/GetHub (**public**)
- **Local path:** `C:\X-Files\Repos\~PERSONAL\GetHub\sourcegit\` (folder still named `sourcegit`)
- **Remote:** `origin` only. **`upstream` was deliberately removed** — do NOT re-add it. (History was orphaned, so upstream can't be merged anyway, and fetching it re-pollutes ~180 SourceGit tags.)
- **History:** rewritten to a single `Forked from SourceGit` root + GetHub commits only. Safety tag `backup-before-history-rewrite` exists locally.

## Tech stack
- .NET 10 SDK (`C:\Program Files\dotnet\dotnet.exe`), Avalonia 11.3.13, C#, `net10.0` WinExe.
- Project: `src/GetHub.csproj`. Solution: `GetHub.slnx`. Version source: `VERSION` file (e.g. `2026.16`).
- Submodule: `depends/AvaloniaEdit`.
- **AOT**: csproj enables `PublishAot`+`PublishTrimmed` for Release unless `-p:DisableAOT=true`. CI builds **AOT**; local `build.ps1` uses **DisableAOT + single-file** (no MSVC needed, fast iteration). Functionally identical.

## THE ONE INSTALL MODEL (important)
`GetHub_Dist/` is the single install — it serves all three needs, and **always preserves `data/`** (prefs/repos):
1. **Dev/debug:** `.\build.ps1` → rebuilds `GetHub_Dist` (single-file). Wipes + rebuilds but backs up/restores `data/`.
2. **Daily use / self-update:** run `GetHub_Dist\GetHub.exe`; "Update Now" swaps the exe in place from the latest release.
3. **Ship to others:** push a tag → CI builds + publishes (others download / self-update).

After a local build the exe is the dev single-file build; after self-update it's the AOT release build — same folder, same prefs. Self-update never downgrades.

## build.ps1
Outputs to `GetHub_Dist/` (gitignored). Auto-closes a running GetHub and relaunches it after.
- `.\build.ps1` — rebuild
- `.\build.ps1 -Run` — rebuild + launch
- `.\build.ps1 -Dist` (or `-Zip`) — also make a **clean shareable zip** `GetHub_<ver>_win-x64.zip` containing ONLY `GetHub.exe` + empty `data/` + `HOW-TO-OPEN.txt` (NO personal data)
- `.\build.ps1 -NoRelaunch` — don't relaunch after
- Build flags under the hood: `-p:DisableAOT=true -p:PublishTrimmed=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`
- On build failure it restores `data/` from the `%TEMP%` backup before exiting.

## Release workflow (push a tag → automatic)
CI (`.github/workflows/release.yml`) triggers on pushing a `v*` tag:
```
# edit VERSION -> e.g. 2026.16, commit, push, then:
git tag -a v2026.16 -m "release notes"   # tag MUST be vMAJOR.MINOR (the v matters)
git push origin v2026.16
```
CI builds **AOT** for win-x64/arm64 + osx-x64/arm64, packages, and **auto-creates the GitHub release** with all 4 zips. Linux was dropped (its packaging broke and canceled releases). Each zip bundles `HOW-TO-OPEN.txt`.
- Tag format is load-bearing: updater does `tag_name.Substring(1)` → `System.Version`.
- Don't manually `gh release create` for a tag CI will handle — it'll conflict.

## Self-update (Windows in-place)
- `src/Models/SelfUpdater.cs` + `src/Views/SelfUpdate.axaml(.cs)`. "Update Now" downloads the asset matching OS/arch (`gethub_<ver>.win-x64.zip`), extracts, then a PowerShell swapper waits for exit, `robocopy`s new files over the install dir **excluding `data/`**, and relaunches.
- Updater endpoint: `api.github.com/repos/GrantDPowellMedrock/GetHub/releases/latest` (anonymous; needs `User-Agent` header — set in `App.axaml.cs`). Requires repo **public**.
- macOS/Linux: falls back to opening the releases page.

## Custom features added on top of SourceGit
Group tabs (folder-based, above the page tabs) with color-coding (right-click) + drag-to-reorder; per-tab ahead/behind indicators; "Open in Zed" toolbar button; version in the Launcher title bar; streamlined About dialog (fork notice, no release-date/copyright); update detection re-pointed at this repo.

## Gotchas
- **Unsigned app** (no code-signing cert). Windows: SmartScreen blocks downloaded exe → right-click zip → Properties → Unblock before extracting, or "More info → Run anyway". macOS: `xattr -cr /Applications/GetHub.app`. Both documented in the bundled `HOW-TO-OPEN.txt`.
- **Single-instance zombie:** GetHub holds `data/process.lock` exclusively; a crashed/headless instance blocks new launches ("process runs, no window"). Fix: kill all `GetHub.exe`, run from a writable folder (not Program Files, not inside the zip preview).
- **Credential / askpass storm:** remotes are HTTPS via git-credential-manager. When the GCM token expires, parallel auto-fetch across many open repos spawns many "Connect to GitHub" prompts. Fix: `gh auth login`. Optionally raise the auto-fetch interval (Preferences → Git).
- **Never re-add `upstream`** (see above).
- `build/` is gitignored but a few tracked files live under it (`build/scripts/*`, `build/resources/*`) — update them with `git add -f`.

## User prefs (Grant)
TypeScript-strict mindset, casual/direct, "do it" = execute. Windows-only, PowerShell. NEVER run DB migrations (n/a here). Build must pass before done.
