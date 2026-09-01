# The 01Skills System — Complete Reference

How the Launcher, the Lobby and the standalone games install, launch and hand control to each
other.

This is the single authoritative document for the whole cross-process system. It is written so
that a person — or a new chat session — can understand and safely modify any part of it
**without opening all three projects**. File references are relative to each project's own
root, because the three repositories live wherever each machine keeps them.

Companion: **`Docs/ADDING_A_GAME.md`** — the step-by-step recipe for splitting a new game into
its own build and linking it into the cycle.

---

## Table of contents

1. [The three applications](#1-the-three-applications)
2. [The command-line contract](#2-the-command-line-contract)
3. [The shared interop layer](#3-the-shared-interop-layer)
4. [The smooth switch (first-frame handshake)](#4-the-smooth-switch-first-frame-handshake)
5. [The intro video rule](#5-the-intro-video-rule)
6. [The games manifest](#6-the-games-manifest)
7. [Auth token and profile](#7-auth-token-and-profile)
8. [Launcher internals](#8-launcher-internals)
9. [The transitions, step by step](#9-the-transitions-step-by-step)
10. [Build and deploy (CDN)](#10-build-and-deploy-cdn)
11. [Testing](#11-testing) — start with the [preflight check](#110-the-preflight-check)
12. [Troubleshooting](#12-troubleshooting)
13. [Known gaps and outstanding work](#13-known-gaps-and-outstanding-work)
14. [Fix log](#14-fix-log)

---

## 1. The three applications

| Role | Repository folder | Unity product name | Executable | Owns |
|---|---|---|---|---|
| **Launcher** | `01SkillsLauncher` | `01Launcher` | `01Launcher.exe` | Install paths, downloads, versions, update checks. Knows *where every build lives on this machine*. |
| **Lobby** (Main) | `ZeroOneDigitalSkills` | `ZeroOneDigitalSkill` | `ZeroOneDigitalSkill.exe` | Login/session, intro video, games section, missions, in-project mini-games. |
| **Game** — Endless Tunnel | `Endless-Tunnel` | `EndlessTunnel` | `EndlessTunnel.exe` | One game. Knows nothing except what it is told on its command line. |
| **Game** — Endless Zig-Zag | `Endless-Zig-Zag` | `ZigZag` | `ZigZag.exe` | Same contract. Note the exe is named after the *product name*, not the catalog id. |

One launcher, one lobby, and one project per separated game — currently four Unity projects
producing four executables. **Only one is ever meant to be visible at a time.** Nothing is
shared at runtime except a command line and two small files on disk.

`RoboRageArena` is still a scene inside the Lobby build, not a separate project. See §6 for
why that distinction is tracked separately from install state.

### The flow

```
                    --launcher=true                    --lobbyPath=  --token=  --config=
   ┌───────────┐    --gamesManifest=   ┌───────────┐   --bot=  --gamesManifest=   ┌────────┐
   │  Launcher │ ──────────────────▶   │   Lobby   │ ─────────────────────────▶   │  Game  │
   │ 01Launcher│    --launcherPath=    │ZeroOneD.S.│                              │E.Tunnel│
   └───────────┘                       └───────────┘   ◀─────────────────────────  └────────┘
         ▲                                   │            --launcher=false --token=
         │                                   │            --gamesManifest=
         └───────────────────────────────────┘
                  exit button
```

Every arrow is: **start the next process → wait until it has drawn its first frame → close this
process.** Section 4 explains why the middle step exists.

---

## 2. The command-line contract

All arguments use the form `--key=value` (**two dashes, no space**), parsed by
`LaunchArgs.Get(key, fallback)`.

> **This exact shape is load-bearing.** A single-dash argument such as `-launchCode=THAKA123`
> reads as *"argument absent"* on the receiving side — it does not error, it silently returns
> the fallback. That is precisely the bug that broke this cycle for months (see §14).

| Argument | Sent by | Read by | Meaning |
|---|---|---|---|
| `--launcher=` | Launcher (`true`), Game (`false`) | Lobby → `LauncherCheck` | `true` = cold start from the launcher → **play the intro**. `false` = a game handed control back → **skip the intro**. Absent = treated as `false`. |
| `--token=` | Launcher, Lobby, Game | Lobby, Game | Auth bearer token. Round-trips through every process so the player never logs in twice. |
| `--gamesManifest=` | Launcher, Lobby, Game | Lobby (games only forward it) | Path to `games.json`. Optional — apps fall back to the default location. |
| `--launcherPath=` | Launcher | Lobby | The launcher's own exe, so the Lobby can return to it on exit. Only sent when the file exists. |
| `--lobbyPath=` | Lobby | Game | The Lobby's own exe, so the Game can return to it. |
| `--config=` | Lobby | Game | Backend game-config name (drives coin rate, energy cost). |
| `--bot=` | Lobby | Game | Robot skin the player selected (`Gravy` / `RN` / `Spark`). |
| `--handshake=` | **every** launch | the app being launched | One-shot GUID for the smooth-switch handshake. **Added automatically by `AppHandoff.SwitchTo` — never write it by hand.** |
| `--endlessTunnelPath=` | Launcher | Lobby | *Legacy.* Superseded by the games manifest; still sent and read as a fallback. |

---

## 3. The shared interop layer

Six files are **duplicated byte-for-byte** across the projects. They are the contract; if the
copies drift, the cycle breaks in ways that only appear in built players.

| File | Purpose |
|---|---|
| `LaunchArgs.cs` | Parses `--key=value` from the command line. |
| `ProcessLauncher.cs` | Starts another exe. Never throws — returns `false` + message. |
| `AppHandoff.cs` | The smooth-switch handshake (§4). `SwitchTo` is the one call you need. |
| `HandoffReadySignaller.cs` | Self-installing; reports *this* app's first frame. No scene setup. |
| `HandoffRunner.cs` | Hosts the wait-then-quit coroutine independently of any scene object. |
| `GamesManifest.cs` | Reads/writes `games.json` and the `GameAvailability` states (§6). |

Where each project keeps them:

| Project | Folder |
|---|---|
| Launcher | `Assets/Scripts/Interop/` |
| Lobby | `Assets/_DEV/BackendAPI/Core/` |
| Game | `Assets/_DEV/Scripts/Core/` |

`GamesManifest.cs` lives only in the Launcher and the Lobby — a game never reads it, it only
passes the path back through.

**To verify the copies still match.** Set `L`, `M` and the game roots to wherever this machine
keeps the repos, then:

```bash
L=01SkillsLauncher/Assets/Scripts/Interop
M=ZeroOneDigitalSkills/Assets/_DEV/BackendAPI/Core
T=Endless-Tunnel/Assets/_DEV/Scripts/Core
Z=Endless-Zig-Zag/Assets/_DEV/Scripts/Core

for f in LaunchArgs ProcessLauncher AppHandoff HandoffReadySignaller HandoffRunner; do
  printf '%-26s %s unique\n' "$f" \
    "$(for d in $L $M $T $Z; do tr -d '\r' < "$d/$f.cs" | md5sum; done \
       | awk '{print $1}' | sort -u | wc -l)"
done
```

Every line must read `1 unique`. The `tr -d '\r'` matters: the copies are byte-identical apart
from line endings, which differ per repo and would otherwise report four "different" files.

### Why `ProcessLauncher` does not use `Process.Start`

On this Unity Mono player, `System.Diagnostics.Process.Start` throws a misleading
`Win32Exception("Success")` — with `UseShellExecute` either `true` (its COM/ShellExecuteEx path
needs an STA apartment, which Unity's main thread never is) or `false`. `ProcessLauncher`
therefore P/Invokes raw Win32 `CreateProcessW` in standalone Windows builds, falling back to
`Process.Start` only in the Editor.

It also **always sets the child's working directory to the child exe's own folder.** Unity
resolves `<exe name>_Data` relative to the current directory, so without this the child
inherits the parent's directory, cannot find its own data folder, and exits instantly with no
error message.

---

## 4. The smooth switch (first-frame handshake)

### The problem

`ProcessLauncher.TryStart()` returns as soon as **Windows has created the process** — seconds
before that process's Unity player has booted, loaded a scene and drawn a pixel. Code that
quits immediately after starting the next app produces one of two bad results:

* quit right away → the user stares at the **desktop** while the next app loads;
* never quit → **two windows** open at once.

### The mechanism

1. The outgoing app mints a one-shot GUID and passes it as `--handshake=<id>`.
2. It starts the next app, then **keeps rendering** and polls for a signal file.
3. The incoming app's `HandoffReadySignaller` — installed via
   `[RuntimeInitializeOnLoadMethod]`, so there is **nothing to wire in any scene** — waits two
   `WaitForEndOfFrame`s (the point at which a frame has actually been presented) and creates
   the signal file.
4. The outgoing app sees the file, deletes it, and quits.

Signal file: `%TEMP%\ZeroOneHandoff\<id>.ready`. A fresh GUID per launch means a stale file
from a crashed run can never satisfy a later handshake.

**Timeout:** if no signal arrives within **25 seconds**, the outgoing app logs a warning and
quits anyway, so a crashed child can never strand a window on screen.

### How to use it

One call does the whole outgoing half:

```csharp
if (!AppHandoff.SwitchTo(exePath, arguments, out string error))
{
    // The launch itself failed. We are still running and still visible —
    // show the error instead of quitting into nothing.
    Debug.LogError(error);
    return;
}
// Do NOT call Application.Quit() here. SwitchTo owns the quit.
```

The incoming half is automatic — copying `HandoffReadySignaller.cs` into the project is the
entire installation.

> **The single most common mistake** is calling `Application.Quit()` after `SwitchTo`. That
> re-introduces the desktop flash. `SwitchTo` quits for you, at the right moment.

`AppHandoff.Quit()` exists for genuine quit-to-desktop cases; it also exits Play mode in the
Editor, so transition code can be tested without building.

### `runInBackground` — why `SwitchTo` sets it

The outgoing app is, by definition, **losing focus** to the window it just opened. In a player
built with *Run In Background* off, Unity pauses the entire player loop on focus loss — which
would freeze the wait forever, **timeout included**, leaving both windows on screen with no way
out.

`AppHandoff.SwitchTo` therefore sets `Application.runInBackground = true` immediately before
starting the child. In code rather than Player Settings so that:

* it works in every project regardless of that checkbox (Endless Tunnel ships with it **off**,
  which would otherwise break every return to the lobby), and
* it is scoped to the handoff — normal gameplay still pauses when the player alt-tabs.

Do not "clean this up" by relying on Player Settings. A new game added later will have the
checkbox off by default and the failure is silent.

---

## 5. The intro video rule

**Requirement:** the intro plays when the player comes in from the Launcher, and is skipped
when they come back out of a game.

Decided by `--launcher=`:

| How the Lobby was started | `--launcher=` | Intro |
|---|---|---|
| From the Launcher | `true` | **Plays** |
| Returning from a game | `false` | **Skipped** |
| Exe opened directly / Editor Play mode | absent → `editorDebugLauncher` field | Follows that field |

`LauncherCheck` exposes `ShouldSkipIntro` and carries **`[DefaultExecutionOrder(-1000)]`** —
load-bearing, not cosmetic. `Intro` reads that static, and Unity gives no ordering guarantee
between components on different GameObjects. Without it, whether the video flashes for a frame
before being skipped comes down to scene load order.

`Intro` guards every exit path (`Update`'s click/Escape, the end-of-video `Invoke`, and
`LauncherCheck`'s skip event) behind a single `_loadStarted` flag, because all three can fire
in the same frame and each used to start its own scene load.

---

## 6. The games manifest

The Lobby cannot know whether a game is installed — that is the Launcher's job. The Launcher
publishes what it knows to a JSON file the Lobby reads.

**Default location:** `%LOCALAPPDATA%\ThakaHoldingGames\games.json` — the same root the launcher
installs into. `--gamesManifest=` overrides it, which makes a scratch file usable for testing.

A fixed path rather than a per-run temp file **on purpose**: it survives the
Lobby → Game → Lobby round trip even if a process forgets to forward the argument.

### Schema

```json
{
  "schemaVersion": 1,
  "writtenUtc": "2026-08-25T12:00:00.0000000Z",
  "games": [
    {
      "id": "endlessTunnel",
      "displayName": "Endless Tunnel",
      "configName": "EndlessTunnel",
      "exePath": "…\\ThakaHoldingGames\\EndlessTunnel\\EndlessTunnel.exe",
      "state": "Ready",
      "localVersion": "1.0.0",
      "remoteVersion": "1.0.0"
    }
  ]
}
```

### States

| `state` | Meaning | Lobby shows |
|---|---|---|
| `Ready` | Installed, verified, launchable now | Play live |
| `UpdateAvailable` | Playable, newer version exists | Play live + "update available" |
| `NotInstalled` | Known but not downloaded | "Get it in the launcher", play blocked |
| `Locked` | Nothing installed **and** nothing reachable to download | Locked visual, play blocked |
| `Unknown` | Launcher has not resolved it yet | "Checking…" |

`exePath` is deliberately **empty** unless the game can actually be launched, so a stale path
can never let the Lobby start a build that is not there.

### The join key

The manifest `id` is what ties a launcher catalog entry to a lobby game card. It comes from the
launcher catalog and is matched **case-insensitively** against the Lobby's `SceneName` member
name — nothing else. The exe name and the backend config name are separate facts and do not
have to match it.

| Launcher catalog `id` | Lobby `SceneName` | `configName` (backend) | `exeName` (product name) |
|---|---|---|---|
| `endlessTunnel` | `EndlessTunnel` | `EndlessTunnel` | `EndlessTunnel.exe` |
| `endlessZigZag` | `EndlessZigZag` | `ZigZag` | `ZigZag.exe` |
| `roborage` | *(none — in-project scene)* | — | — |

`roborage`'s id **deliberately does not match** `SceneName.RoboRageArena`. Giving it a matching
id would make the Lobby resolve it through the manifest, treat it as a separate executable, and
stop loading the scene that actually ships in the Lobby build.

### Who writes it

`MenuManager.PublishGamesManifest()`, at three moments:

1. after the startup catalog refresh,
2. after any download/update finishes (so a game installed while the launcher is open is
   already `Ready`),
3. immediately before handing over to the Lobby.

Writes are **write-to-temp-then-move**, because the Lobby may be reading the file at the same
moment; a half-written file parses as an empty games list and shows everything unavailable.

### Who reads it

`LauncherCheck.Awake()` loads it into the static `LauncherCheck.Games`. From there:

* `GamesHandler.ExternalGame(SceneName)` → the manifest entry, or `null` for an in-project scene
* `GamesHandler.CanStart(SceneName)` → whether the player may press play
* `GameCardState` (on each card) renders the state and blocks the play button

**Externality vs availability are tracked separately, on purpose.** `GamesHandler` has an
**External Games** list naming which `SceneName`s ship as their own executable. The manifest
says *how a game is doing*; the list says *what kind of thing it is*. If the manifest is
missing — lobby opened directly, the Editor, or the launcher has not published yet — an
undeclared game would fall through to `loader.LoadScene()` and fail on a scene that does not
exist in that project.

---

## 7. Auth token and profile

The player logs in **once**; the token rides every command line thereafter:

```
Launcher ──--token=──▶ Lobby ──--token=──▶ Game ──--token=──▶ Lobby ──▶ …
```

`LauncherCheck.Awake()` applies the token **regardless of `--launcher=`**. This is deliberate
and was a real bug: the token used to be applied only inside an `if (launchedFromLauncher)`
block, so every game handing control back with `--launcher=false` had its token thrown away,
dropping the player into the lobby logged out immediately after playing as themselves.

### The profile cache

The user profile (name, email, school, team) **only ever arrives inside the `/login` response.**
There is no endpoint that returns it for an existing token — the full API surface is `/login`,
`/register`, `/session` (Microsoft OAuth device-flow), `/forget-password`, `/reset-password`,
`/verify-email`, `/games/*`, and the progress endpoint. None of them rebuild a profile.

So on the return path the lobby comes up authenticated with **progress but no profile** — coins
and energy correct, username blank.

`PlayerDataStore` therefore caches the profile to `PlayerPrefs` on every `SetProfile`, and
`LauncherCheck` restores it alongside the token. Three details matter:

* **Cleared on logout** (`Clear()` wipes the cache), or the next user to sign in on that machine
  would see the previous user's name reappear on their first return from a game.
* **`TryRestoreCachedProfile()` is a no-op when a profile is already loaded**, so a real login
  always wins over the cache.
* **A serializable DTO mirrors the fields** — `UserProfile` has private setters and no
  `[Serializable]`, so `JsonUtility` cannot round-trip it directly.

> **Security note.** The token is passed as a plain command-line argument, visible to any
> process that can enumerate command lines (Task Manager's "Command line" column, WMI). A known
> trade-off of the current design. To harden it, move the token into the same on-disk channel
> the games manifest already uses, with permissions restricted to the current user.

---

## 8. Launcher internals

### 8.1 Entry kinds

`LauncherEntryKind` — one catalog, three behaviours:

| Kind | Installed by launcher | Started by | Gets a Start button |
|---|---|---|---|
| `Project` | ✅ | **the launcher** | ✅ |
| `Game` | ✅ | **the lobby** (via games manifest) | ✗ — shows Remove instead |
| `Addon` | ✅ | nothing (content pack) | ✗ |

`Launchable` is derived from `kind` rather than stored, because the Inspector's list "+" button
copies the previous element — a separate flag would silently give an addon a Start button for a
folder holding no exe.

### 8.2 Install paths — `PathManager`

Each entry has its own install folder, independently relocatable, keyed by `id`.

* Default: `%LOCALAPPDATA%\ThakaHoldingGames\<InstallFolderName>`
* Saved per entry in `PlayerPrefs` under `InstallPath_<id>` (the Windows registry)
* **Renaming an `id` orphans the user's existing install** — it keys the saved path

| Method | Use |
|---|---|
| `PeekPath(id)` | Resolve **without** creating the folder or running icacls — for display |
| `GetPath(id)` | Resolve, create, and repair permissions — for real use |
| `IsCustomPath(id)` | User-picked folder vs default |
| `TrySetPathFor(id, path, out error)` | Validate + save, no dialogs |
| `SetPathFor(id)` | Native folder picker (button-friendly) |
| `ResetPathFor(id)` | Back to default (button-friendly) |
| `OpenFolderFor(id)` | Open in Explorer (button-friendly) |
| `Confirm(text, caption)` | Win32 yes/no, for destructive actions |
| `PathChanged` event | Fires with the entry id so UI redraws |

Folder validation rejects: `AppData\LocalLow` (Windows gives that tree a Low integrity label,
which silently blocks `ShellExecute` and temp writes), `Program Files`, `Windows`, non-writable
folders, and **a folder already used by another entry** — two entries sharing a folder would
make each one's sync delete the other's files, since anything missing from its own manifest is
pruned as stale.

`PathManager` uses Win32 `MessageBoxW`, never WinForms. Constructing any WinForms control
installs a `WindowsFormsSynchronizationContext` as `SynchronizationContext.Current`, permanently
replacing Unity's — every later `await` then posts its continuation to a message loop Unity
never pumps, silently abandoning the work. `RestoreUnityContext()` exists because
`StandaloneFileBrowser` still uses WinForms internally.

### 8.3 Per-entry UI

Every catalog entry carries its own UI references, so the launcher can show a full row per
product without any list-building code.

| Field | Type | Behaviour |
|---|---|---|
| `pathButton` | Button | Opens the folder picker. Disabled for coming-soon entries |
| `pathText` | TMP_Text | Current install folder, or "Coming soon" |
| `downloadObj` | GameObject | Shown when `NeedsDownload` |
| `updateObj` | GameObject | Shown when `NeedsUpdate` |
| `cancelObj` | GameObject | Shown while **this** entry is downloading |
| `startObj` | GameObject | **Projects only** — shown when launchable |
| `removeObj` | GameObject | **Games/addons only** — shown when installed |
| `fetchingObj` | GameObject | Shown only during the server-check phase |
| `loadingObj` | GameObject | Shown while checking **or** downloading |
| `progressObj` | GameObject | Bar container, shown while downloading |
| `fillBar` | Image | Driven 0→1 (Image Type must be **Filled**) |

**Leave every OnClick list empty.** `MenuManager` finds the Button/Button2 inside each object —
searching inactive children, since they start hidden — and hooks it with the correct entry id.
Hooking in code keeps the id in one place; a hand-typed string argument per button fails
silently at runtime when mistyped.

> **Do not do both.** Assigning a `...Obj` *and* wiring its OnClick manually makes the click
> fire twice: `HookClick` adds a runtime listener and `RemoveAllListeners()` clears only runtime
> ones, so an Inspector persistent call survives alongside it. Harmless (the second call hits a
> guard) but noisy. Assign the object and leave OnClick empty, **or** wire manually and leave
> the field empty — which loses the automatic show/hide.

**Visibility rules:**

| Entry state | download | update | cancel | start / remove | fetching | loading | progress |
|---|---|---|---|---|---|---|---|
| Checking | ✗ | ✗ | ✗ | ✗ | ✅ | ✅ | ✗ |
| NeedsDownload | ✅ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| NeedsUpdate | ✗ | ✅ | ✗ | ✗ | ✗ | ✗ | ✗ |
| UpToDate / OfflineReady | ✗ | ✗ | ✗ | ✅ | ✗ | ✗ | ✗ |
| Downloading | ✗ | ✗ | ✅ | ✗ | ✗ | ✅ | ✅ |
| Locked | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ |
| Coming soon | all off |

**While an entry is downloading, Cancel is its only action** — starting a second download or
deleting the files being written into would corrupt the install in progress.

The spinner shows on `Checking` but **not** `Unknown`: with `checkAllEntriesOnStartup` off,
unfetched entries stay `Unknown` forever and would spin all session.

### 8.4 "Coming soon" entries

An entry with a blank **Install Folder Name** is a placeholder for a product that is not
shipping yet — `LauncherEntryConfig.ComingSoon`. No extra flag to keep in sync.

Its path label shows the `Coming Soon Text` (a serialized field on `MenuManager`, default
`"Coming soon"`), its browse button is disabled **and its click is never hooked** — disabling
alone is only a visual, and anything re-enabling the button later would open a picker for a
product with nowhere to install. All action buttons are hidden.

`FolderName` still falls back to the `id` for these, so anything that *does* resolve a path gets
a sane, non-colliding folder rather than an empty string.

### 8.5 Actions

| Method | Notes |
|---|---|
| `StartUpdateFor(id)` | **Download and Update are the same method** — installs when nothing is there, patches when it is. Two buttons exist only for different labels. |
| `CancelUpdateFor(id)` | Only acts if *that* entry is the one running |
| `RemoveEntryFor(id)` | Deletes installed files. Confirms first; refuses mid-download |
| `StartGameFor(id)` | Projects only |
| `StartFetchFor(id)` | Re-check against the server |
| `SelectEntry(id)` | Change the active entry |

**Cancellation** is threaded from `MenuManager` → `LauncherEntry.InstallOrUpdateAsync(ct)` →
`ServerFetcher.UpdateGame(…, ct)` → `DownloadBlobs`/`DownloadAndExtractZip` → `DownloadFile`.

In `DownloadFile` the request is **aborted before deleting** the partial file —
`DownloadHandlerFile` holds it open, so deleting first fails on Windows and leaves a partial
that later reads as complete. Cancelling between blobs is clean: verified files stay and the
next run's hash pass skips them, so a cancelled download **resumes** rather than restarting.

**Remove** (`LauncherEntry.RemoveInstall`) deletes the folder *contents* but keeps the folder,
so the saved path survives a remove/reinstall cycle and the "folder already used by X" check
still works. It also drops the cached `Fetcher`, whose plan describes files that no longer
exist.

### 8.6 Editor drawer

`Assets/Scripts/Editor/LauncherEntryConfigDrawer.cs` (Editor-only, ships in no build) draws each
catalog entry as one collapsed row titled by name and kind:

```
▶ 01Skills        (Project)
▶ xRC             (Project — Coming soon)
▶ Endless Tunnel  (Game)
```

Entries are collapsed by default, and "Coming soon" is surfaced in the header because an empty
Install Folder Name silently changes behaviour and is easy to miss once the field is hidden.

> A `PropertyDrawer` takes over the whole element, so Unity stops rendering `[Header]`
> attributes on the fields. The three section titles are re-declared in the drawer's
> `SectionBefore` map — **add a new `[Header]` there too or it will not show.**

---

## 9. The transitions, step by step

### 9.1 Launcher → Lobby — `MenuManager.LaunchEntry()`

1. Guard against a double click (`_isHandingOff`).
2. Resolve the exe (`FindExePath()`); if none, show the locked state and stop.
3. `PublishGamesManifest()` — the Lobby reads it during its own startup.
4. `AppHandoff.SwitchTo(exe, "--launcher=true --gamesManifest=… [--launcherPath=…] [--endlessTunnelPath=…]")`.
5. On success: disable buttons and let `SwitchTo` quit once the Lobby is on screen. On failure:
   stay open and log the error.

`--launcherPath=` is **only sent when the file exists**. In the Editor,
`Application.dataPath` is `<project>/Assets`, so the derived path is `<project>/01Launcher.exe`,
which does not exist — the Lobby would then fail with Win32 error 2 trying to return. Omitting
it makes the Lobby quit to the desktop instead, which is correct when there is no launcher.

### 9.2 Lobby → Game — `GamesHandler.StartGame(configName, sceneName)`

1. **Pre-flight:** if this is an external game and not launchable, stop **before** calling
   `/games/start`. Starting a game consumes energy server-side, so discovering a missing install
   afterwards would charge the player for a game that never opened.
2. `/games/end` then `/games/start` (the energy gate).
3. `ExternalGame(sceneName)` decides separate-build vs in-project scene:
   * manifest entry → separate build with real install state;
   * no entry but listed in **External Games** → separate build with a synthesized entry,
     falling back to `--endlessTunnelPath=` / the Editor debug field;
   * neither → in-project scene, `loader.LoadScene(sceneName)`.
4. `AppHandoff.SwitchTo(exe, "--token=… --config=… --lobbyPath=… --bot=… --gamesManifest=…")`.
5. Show the loading screen and let `SwitchTo` quit — otherwise the player stares at a fully
   interactive lobby whose clicks land on a screen about to vanish.

### 9.3 Game → Lobby — `PlayerHandler.ReturnToLobby()`

1. Read `DataTransferHandler.lobbyPath` (from `--lobbyPath=`).
2. `AppHandoff.SwitchTo(lobbyPath, "--launcher=false --token=… --gamesManifest=…")`.
   * `--launcher=false` skips the intro on the way back in.
   * `--gamesManifest=` is handed straight back, or the Lobby returns with an empty games
     section.
3. No lobby path, or launch failed → `AppHandoff.Quit()`. Staying open would strand the player
   in a game they asked to leave.

### 9.4 Lobby → Launcher — `LauncherCheck.ReturnToLauncher()`

Static, and hosts its own wait, because the exit buttons that call it sit on panels the
transition itself tears down, and in mission scenes where no `GamesHandler` exists.

Wired from `LogOutHandler.QuitGame("Quit")`, `MainPanelManager.CloseApplication()`, and
`GamesHandler.ReturnToLauncher()` (kept for existing scene wiring). Falls back to a plain quit
when `--launcherPath=` was never passed.

---

## 10. Build and deploy (CDN)

### 10.1 Build

Build each product into its own folder, e.g. `Builds/MainLobby/`, `Builds/EndlessTunnel/`, and
zip that folder's **contents** (files at the archive root, no wrapping folder).

> **Never use a Script Only Build for a release.** It rebuilds managed assemblies but explicitly
> **does not rebuild player data — which is where scenes live.** A script-only build completes in
> seconds and logs `player data was not rebuilt`; any Inspector/scene change silently will not
> ship. A real IL2CPP build takes minutes.

> **Delete `*_BurstDebugInformation_DoNotShip`** before generating a manifest. ManifestBuilder's
> skip rules match on **file name only**, so a `BurstDebugInformation` *folder* is not caught and
> its contents would ship.

### 10.2 Generate the manifest

```powershell
ManifestBuilder.exe "<build folder>" "<staging folder>" --version "1.0.0" --channel "stable"
```

Built once via `dotnet publish ManifestBuilder/ManifestBuilder.csproj -c Release`.

**Omit `--cdn-url` deliberately.** When `cdnBaseUrl` is absent, `ServerFetcher` resolves blobs
as `{manifestFolderUrl}/files/{sha256}` — relative to wherever `manifest.json` is served. That
makes the manifest domain-agnostic and survives a CDN move without regenerating.

Do **not** use `--prefix` — the launcher installs files at the install root.

Output:

```
<staging>/manifest.json     ← upload to the product's CDN folder
<staging>/files/            ← content-addressed blobs, named by SHA-256
```

Verify: `manifestVersion: 2`, the right `appVersion`, the main exe at the root with no prefix,
forward-slash paths, and no `CrashHandler` entries. Blob count may be *lower* than the entry
count — identical files share one blob.

### 10.3 Upload — blobs, then zip, then manifest

```powershell
aws s3 sync "<staging>\files" s3://<bucket>/<Product>/files/ `
  --cache-control "public, max-age=31536000, immutable"

aws s3 cp "<build>\<Product>.zip" s3://<bucket>/<Product>/<Product>.zip

aws s3 cp "<staging>\manifest.json" s3://<bucket>/<Product>/manifest.json `
  --cache-control "no-cache"
```

**Order matters.** A manifest referencing blobs that are not up yet gives 404s mid-install.

**Upload the zip.** `ServerFetcher` picks it whenever the install folder is empty *or* the zip
is smaller than the per-file download — for a 1.4 GB product with a 487 MB zip it always wins on
a first install, so skipping it makes every fresh install ~3× slower.

| Resource | Cache-Control | Why |
|---|---|---|
| `files/{hash}` | `public, max-age=31536000, immutable` | Hash-named — never changes |
| `manifest.json` | `no-cache` | Must always be fresh |

### 10.3b The live layout

Everything is served from one CloudFront distribution, one folder per product, each holding a
`…CDN/` manifest folder and a full-install zip:

| Product | Catalog entry | `Manifest Folder Url` | `Zip File Url` |
|---|---|---|---|
| Lobby | `01skills` | `…/Main-Lobby/MainLobbyCDN` | `…/Main-Lobby/MainLobby.zip` |
| Endless Tunnel | `endlessTunnel` | `…/Mini-Games/Endless-Tunnel/EndlessTunnelCDN` | `…/Mini-Games/Endless-Tunnel/EndlessTunnel.zip` |
| Endless Zig-Zag | `endlessZigZag` | `…/Mini-Games/Endless-Zig-Zag/EndlessZigZagCDN` | `…/Mini-Games/Endless-Zig-Zag/EndlessZigZag.zip` |
| Launcher itself | *(self-update)* | `…/NewLauncher/version.txt` | `…/NewLauncher/01Launcher.zip` |

`…` is `https://d1yb6h2kf7dolp.cloudfront.net`. Each project's local `Builds/` folder mirrors
that layout — `Builds/<Name>/` is the raw build, `Builds/<Name>.zip` the archive, and
`Builds/<Name>CDN/` the ManifestBuilder staging folder that gets synced up.

The launcher's own update channel is a plain `version.txt` plus a zip, not a manifest: it
compares the text file against `CurrentLauncherVersion` and replaces itself. Bump
`CurrentLauncherVersion` in the same commit that uploads a new `01Launcher.zip`, or the new
build will keep offering itself an update.

### 10.4 Point the catalog at it

On the entry, set:

* `Manifest Folder Url` → `https://<host>/<Product>/`
* `Zip File Url` → `https://<host>/<Product>/<Product>.zip`

> The scene's serialized `Catalog` **overrides the C# initialiser**, so set these in the
> Inspector (or edit the `Catalog:` block in `Assets/Scenes/Launcher.unity`). `kind:` is written
> as an integer there — `0` = Project, `1` = Addon, `2` = Game.

### 10.5 How updates stay small

The launcher hashes every local file against the manifest and downloads only what differs.
Unchanged files are never re-downloaded; a renamed file with identical content costs 0 bytes
(same hash = same blob). `version.txt` is written into the install folder and excluded from
stale-file pruning.

Note: stale-file pruning runs on the **blob path only**, not after a zip extract. A file present
in the zip but absent from the manifest (e.g. `UnityCrashHandler64.exe`) survives a fresh
install and is cleaned on the first incremental update. Deletions do not count toward download
bytes, so this never produces a phantom "update available".

---

## 11. Testing

### 11.0 The preflight check

`01SkillsLauncher/Docs/verify-chain.py` reads all four projects' serialized data and asserts
every join the compiler cannot see: the catalog-id / `SceneName` / config-name chain, that no
in-project scene is shadowed by a catalog id, that each separated game's `exeName` matches its
Unity Product Name, that the six interop files are byte-identical, and that the Lobby no longer
builds the scenes that moved out.

```bash
python 01SkillsLauncher/Docs/verify-chain.py \
  --launcher <path> --lobby <path> --tunnel <path> --zigzag <path>
```

It exits non-zero on the first failure, so it can gate a release. **Run it after adding or
renaming a game, and before every upload** — the failures it catches (an id that matches
nothing, a `configName` that disagrees with the game's own scene) are all silent at runtime.

### 11.1 Editor debug fields

Every app has Editor-only fallbacks, used **only** when the matching `--arg=` is absent.

**Lobby** — `LauncherCheck` on the `LauncherCheck` object in the `Intro` scene:

| Field | Use |
|---|---|
| `editorDebugLauncher` | `true` = cold start from launcher (intro plays); `false` = returning from a game |
| `editorDebugToken` | A JWT to run authenticated |
| `editorDebugEndlessTunnelPath` | Local `EndlessTunnel.exe` |
| `editorDebugGamesManifestPath` | A scratch `games.json` to test card states |

**Game** — `PlayerHandler`: `editorDebugToken`, `editorDebugBot`, `editorDebugLobbyPath`.

### 11.2 Card-state fixtures

`Builds/_CardStateTests/` holds a fake `games.json` per state plus `test-card.cmd`:

```cmd
test-card.cmd ready            :: normal playable cards
test-card.cmd update           :: update-available visual
test-card.cmd notinstalled     :: blocked, "get it in the launcher"
test-card.cmd locked           :: locked visual
test-card.cmd unknown          :: "checking..." visual
test-card.cmd none             :: no manifest at all
test-card.cmd ready intro      :: same, but play the intro
```

Each opens the lobby build with a fake manifest, so no install/uninstall is needed to see a
state. It auto-detects the build in `Builds\` or `Builds\MainLobby\`. Every fixture carries
both separated games (`endlessTunnel` and `endlessZigZag`), so one run shows both cards.

A `Ready` card only needs a **non-empty** `exePath` to render — the file need not exist. Clicking
Play then fails, which is fine for testing rendering.

> `Builds/` is gitignored, so this folder is a **local** helper and does not travel with a
> clone. If it is missing, recreate it: five `games.<state>.json` files matching the schema in
> §6, plus the launcher script above.

### 11.3 The handshake

**Cannot be tested in the Editor** — there is no second process to signal it. Build all three
and walk the loop:

| # | Step | Expected |
|---|---|---|
| 1 | Launcher → Start the Lobby | Intro **plays**; launcher disappears only *after* the lobby is visible. No desktop flash, never two windows |
| 2 | Open the games section | Each card shows the right state; uninstalled games not clickable |
| 3 | Start the separated game | Lobby shows loading, game appears, lobby closes. Right bot, still logged in |
| 4 | Quit / return to lobby | Lobby reappears, intro **skipped**, **username still shown**, games section populated |
| 5 | Exit the lobby | Launcher comes back |
| 6 | Uninstall the game, reopen | Card shows "not installed", refuses to start, **no energy consumed** |

### 11.4 Where the logs are

Unity writes to `%USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\Player.log`, so
each app has its own — note the launcher's company name differs from the rest.

| App | Log |
|---|---|
| Launcher | `%USERPROFILE%\AppData\LocalLow\ThakaHolding\01Launcher\Player.log` |
| Lobby | `%USERPROFILE%\AppData\LocalLow\Thaka\ZeroOneDigitalSkill\Player.log` |
| Endless Tunnel | `%USERPROFILE%\AppData\LocalLow\Thaka\EndlessTunnel\Player.log` |
| Endless Zig-Zag | `%USERPROFILE%\AppData\LocalLow\Thaka\ZigZag\Player.log` |
| Unity build errors | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |

Player logs are **overwritten each run** — capture immediately after reproducing.

---

## 12. Troubleshooting

| Symptom | Cause |
|---|---|
| Intro plays when returning from a game | `--launcher=false` not sent, or an old `LauncherCheck` |
| Intro skipped on a cold start | Launcher not sending `--launcher=true` — check for a single-dash argument |
| Desktop visible during a switch | Something calls `Application.Quit()` after `SwitchTo`, or `HandoffReadySignaller.cs` is missing from the incoming app |
| Two windows open at once | Incoming app never signalled; outgoing app is waiting out its 25 s timeout |
| Two windows open **forever** | Outgoing player loop paused on focus loss — `SwitchTo` must set `runInBackground` |
| Game closes instantly on launch | Wrong working directory, so Unity cannot find `<exe>_Data`. Use `ProcessLauncher` |
| Player logged out after a game | `--token=` not passed back, or `LauncherCheck` gating the token on `--launcher=` |
| **Username blank after a game** | Profile cache missing — see §7 |
| Games section empty after returning | `--gamesManifest=` not handed back by the game |
| Card always "Checking…" | No `games.json`, or the `id` does not match the `SceneName` member |
| Lobby tries to load a missing scene | Game not listed in `GamesHandler`'s **External Games** |
| Button does nothing, nothing in the log | Its OnClick target is `{fileID: 0}` — the component was deleted from the scene |
| Inspector refuses to accept a button | Field typed `Button`, but the UI uses Events 2.0 `Button2`, which derives from `Selectable`, **not** `Button` |
| Scene/Inspector changes not in the build | Script Only Build — player data was not rebuilt |
| Launcher shows Update instead of Start | Its `manifestFolderUrl` points at a manifest that does not match the install |
| 404 on blob download | Manifest uploaded before the blobs |

---

## 13. Known gaps and outstanding work

**Must do before shipping:**

* **Upload the three products before the launcher goes out.** Every catalog entry now points at
  a real CloudFront URL (§10.3b). An entry whose manifest 404s resolves to `OfflineReady` when
  something is already installed and `Locked` when nothing is — so a launcher shipped ahead of
  the uploads shows locked cards rather than an error. Order within a product still matters:
  blobs, then zip, then manifest.
* **Never point `01skills` back at `download.01skills.com/TestingForLauncher/T2.0/`.** It still
  serves an `EmptyProjectForUpload.exe` placeholder, and pointing real installs at it would
  "update" them down to that stub.

**Lower priority:**

* `xrc` and `roborage` are coming-soon placeholders with no install folder. `roborage` is a
  scene in the Lobby build, so it needs no launcher entry at all — its card is cosmetic.
* The launcher's game cards have a `Locked` overlay that nothing in `MenuManager` drives; it is
  a hand-toggled "coming soon" visual. `LauncherEntryConfig` has no `lockedObj` field, so an
  entry that resolves to `EntryStatus.Locked` simply shows no buttons.
* Both mini-game cards in the launcher still show placeholder `DESCRIPTION` body text.
* `ShowState` and `RenderActive` in `MenuManager` are vestigial — their global state fields were
  removed, `ShowState`'s body is now only the bar-group toggle. Safe to delete entirely.
* `--endlessTunnelPath=` is legacy. New games use the manifest and should not add per-game
  arguments.
* Coming-soon entries are still fetched at startup and create an empty folder named after their
  id. Harmless, but they could be skipped entirely.
* The energy pre-flight only covers external games; in-project mini-games still consume energy
  before their scene loads — fine, because a scene load cannot fail the way a missing install can.
* `GameCardState.DescribeState` is unused since the status label was dropped.

---

## 14. Fix log

For context when reading git history.

| Bug | Was | Now |
|---|---|---|
| **Intro always skipped** | Launcher sent `-launchCode=THAKA123` — single dash, a key nothing reads. `--launcher=` was therefore always absent, so `LauncherCheck` defaulted to "not from launcher" and skipped every time | Launcher sends `--launcher=true` |
| **Desktop flash between apps** | Every transition called `Application.Quit()` immediately after starting the next process | `AppHandoff.SwitchTo` holds the outgoing app on screen until the incoming app reports its first frame |
| **Handoff could hang forever** | A player with *Run In Background* off pauses its loop on focus loss, freezing the wait and its timeout | `SwitchTo` sets `runInBackground` before starting the child |
| **Launcher never closed** | `LaunchEntry()` started the process and returned | Closes via the handshake, after the Lobby is on screen |
| **Token dropped on return** | `LauncherCheck` applied `--token=` only when `--launcher=true` | Applied on every path |
| **Username blank on return** | Profile only ever arrives in the `/login` response; no endpoint rebuilds it from a token | `PlayerDataStore` caches it, `LauncherCheck` restores it, logout clears it |
| **No games data reached the Lobby** | Lobby had no way to know what was installed | Launcher publishes `games.json`; `GameCardState` renders it |
| **Missing manifest broke external games** | Externality was inferred from the manifest alone, so a missing manifest sent the lobby down `LoadScene` for a non-existent scene | **External Games** list declares externality; the manifest only supplies state |
| **Energy burned on a missing game** | `/games/start` ran before the exe was checked | Pre-flight check before the energy call |
| **Double scene loads from the intro** | Click, end-of-video `Invoke`, and the skip event each started their own load | Single `_loadStarted` guard; `CancelInvoke` on exit |
| **Intro video flashed when skipping** | `Start()` called `videoPlayer.Play()` unconditionally, then a sibling stopped it a frame later | Never played on the skip path |
| **Blocking load on the first frame** | `SkipIntro()` used raw `SceneManager.LoadScene` | Goes through `Loadermanger` like every other transition |
| **`Process.Start` in the Launcher** | Used the API the other two projects had abandoned for throwing `Win32Exception("Success")` | Uses the shared `ProcessLauncher` (`CreateProcessW`) |
| **Double-click started two copies** | No guard on the launch buttons | `_isHandingOff` guards every transition |
| **Quitting the Lobby went to the desktop** | `Application.Quit()` on the exit buttons | Returns to the Launcher when it started us |
| **`--launcherPath` broke Editor runs** | Derived path does not exist in the Editor; the Lobby failed with Win32 error 2 on exit | Only sent when the file exists |
| **Minimize minimized the Unity Editor** | `#if UNITY_STANDALONE_WIN` is defined in the Editor when the build target is Windows | Guard is now `&& !UNITY_EDITOR`; `GetActiveWindow()` replaces title matching |
| **Launcher window buttons were dead** | All four OnClick targets were `{fileID: 0}` — the `TopBarSettings` component had been deleted from the scene | Component restored and re-assigned; method names kept so the calls re-resolve |
| **No cancel** | `ServerFetcher` had no cancellation at all | `CancellationToken` threaded end to end; aborts before deleting the partial file |
| **No remove** | No uninstall anywhere | `RemoveInstall()` clears contents, keeps the folder, drops the stale plan |
| **Catalog unusable in the Inspector** | ~20 fields × 5 entries, all expanded, labelled "Element 0" | Editor drawer: collapsed, titled by name and kind |
| **Zig-Zag never reachable from the Lobby** | The launcher entry's id was `zigzag`, which matches no `SceneName` member, so the manifest lookup missed and the Lobby fell through to a scene the build no longer contains | Id is `endlessZigZag`, matching `SceneName.EndlessZigZag` |
| **Zig-Zag card's buttons were dead** | Its Add/Remove/Cancel objects were plain Images with no `Button` component, so `HookClick` found nothing to hook; there was no Update object at all | Buttons added (OnClick lists left empty, as `MenuManager` hooks them), Update object created |
| **Zig-Zag ignored the backend coin rate** | `GameManager.Start()` read `GetConfig()` without ever calling `FetchConfigs` — the Lobby's configs do not survive the process switch, so it always found nothing and kept the serialized default | `LoadConfig_Co()` fetches first, mirroring `PlayerHandler` in Endless Tunnel |
| **Installer build broken by the repo move** | `INNO.iss` hard-coded `D:\Willy\Projects\01SkillsLauncher`, which stopped existing | Paths derive from the `.iss` file's own location |
