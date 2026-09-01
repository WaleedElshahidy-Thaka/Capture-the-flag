# Separating a Game Into Its Own Build

The recipe for taking a mini-game out of the Lobby project (or starting a new one) and linking
it into the launcher → lobby → game cycle.

Read **`Docs/APP_CYCLE.md`** first for how the cycle works. This document is the checklist.

All paths are relative to each project's own root — the three repositories live wherever each
machine keeps them.

---

## Why separate a game at all

A game in its own build keeps its assets, packages and build time out of the Lobby. The cost
is that starting it becomes a **process switch** instead of a scene load, which is what the
whole handshake system exists to make invisible.

**Games can stay as scenes in the Lobby.** `RoboRageArena` does, and it works fine —
`GamesHandler` loads it with `loader.LoadScene(sceneName)`. Only separate a game when its size
or build time actually justifies it. `EndlessTunnel` and `EndlessZigZag` are separated;
`RoboRageArena` is not.

---

## The naming rule that ties everything together

One string is used as the join key in three places, and they **must match**:

```
Launcher catalog entry id   ──┐
                              ├── all three identical (case-insensitive)
Lobby SceneName enum member ──┤
games.json "id" field       ──┘
```

For Endless Tunnel that string is `endlessTunnel` / `EndlessTunnel`; for Zig-Zag it is
`endlessZigZag` / `EndlessZigZag`.

**Only the id has to match.** The exe name comes from the game project's Unity *Product Name*
and the `configName` from the backend — neither is derived from the id, and for Zig-Zag both
differ from it (`ZigZag.exe`, config `ZigZag`). Set them explicitly on the catalog entry.

Get this right and the Lobby needs **no code changes at all** to launch a new game — it
resolves external games by matching the `SceneName` against the manifest. Get it wrong and the
Lobby will silently try to load a scene that does not exist.

---

## 1. In the game project

### 1.1 Copy the interop files

Copy these five files from an existing game (or from the Launcher's `Assets/Scripts/Interop/`)
into your game's `Assets/_DEV/Scripts/Core/`:

```
LaunchArgs.cs
ProcessLauncher.cs
AppHandoff.cs
HandoffReadySignaller.cs
HandoffRunner.cs
```

Copy them **byte for byte** — do not "tidy" them. They are the contract between processes, and
a difference in one copy produces failures that only appear in built players. `GamesManifest.cs`
is *not* needed in a game; a game never reads the manifest.

`HandoffReadySignaller` installs itself through `[RuntimeInitializeOnLoadMethod]`. Dropping the
file in is the entire installation — **no prefab, no scene object, nothing to forget.**

### 1.2 Read the launch arguments

In your main controller's `Awake()`:

```csharp
private void Awake()
{
    AuthTokenStore.Instance.Token   = LaunchArgs.Get("token", editorDebugToken);
    DataTransferHandler.lobbyPath   = LaunchArgs.Get("lobbyPath", editorDebugLobbyPath);
    DataTransferHandler.gamesManifestPath = LaunchArgs.Get("gamesManifest", "");
    DataTransferHandler.selectedBotName   = LaunchArgs.Get("bot", DataTransferHandler.selectedBotName);
    GameName = LaunchArgs.Get("config", GameName);
}
```

Give each one an Editor-only fallback field so the game is still runnable in Play mode:

```csharp
[Header("Editor Testing")]
[Tooltip("Used only when no --token= command-line argument is present (e.g. running in the Editor).")]
[SerializeField] string editorDebugToken;
[Tooltip("Used only when no --lobbyPath= command-line argument is present.")]
[SerializeField] string editorDebugLobbyPath;
```

`gamesManifestPath` is stored **only to hand back** on the way out — the game never reads it.
Skip it and the Lobby returns with an empty games section.

### 1.3 Return to the lobby

```csharp
public void ReturnToLobby()
{
    Time.timeScale = 1;   // pause menus are the usual entry point

    string lobbyPath = DataTransferHandler.lobbyPath;
    if (!string.IsNullOrEmpty(lobbyPath))
    {
        // --launcher=false is what tells the lobby to SKIP its intro video: the player
        // already watched it on the way in.
        string arguments = $"--launcher=false --token={AuthTokenStore.Instance.Token}";
        if (!string.IsNullOrEmpty(DataTransferHandler.gamesManifestPath))
            arguments += $" --gamesManifest=\"{DataTransferHandler.gamesManifestPath}\"";

        if (AppHandoff.SwitchTo(lobbyPath, arguments, out string error))
            return;   // SwitchTo owns the quit — do NOT quit here

        Debug.LogError($"Failed to launch lobby at '{lobbyPath}': {error}");
    }

    AppHandoff.Quit();   // nothing to return to, or the launch failed
}
```

**Rules:**
* Never call `Application.Quit()` after a successful `SwitchTo` — that re-creates the desktop
  flash the handshake exists to prevent.
* Always quit on the failure path. Staying open strands the player in a game they asked to
  leave.
* Wire every exit button (pause menu, game-over screen) to this one method.

### 1.4 Build settings

* Set **Product Name** to the exe name you want; it becomes `<ProductName>.exe`.
* Build to the project's own `Builds/` folder.

---

## 2. In the Launcher

Add a catalog entry. Either in the `MenuManager` inspector on `Assets/Scenes/Launcher.unity`,
or in the `Catalog` initialiser in `Assets/Scripts/MenuManager.cs`.

```csharp
new LauncherEntryConfig
{
    id               = "endlessTunnel",        // ← the join key, must match SceneName
    displayName      = "Endless Tunnel",
    kind             = LauncherEntryKind.Game, // ← NOT Project
    installFolderName= "EndlessTunnel",
    exeName          = "EndlessTunnel.exe",
    configName       = "EndlessTunnel",        // backend game-config name
    manifestFolderUrl= "https://…/EndlessTunnel/",   // CDN folder holding manifest.json
},
```

| Field | Notes |
|---|---|
| `id` | The join key. **Renaming it later orphans every existing install**, because it keys the saved install path. |
| `kind` | `Game` = installed and updated by the launcher, **launched by the lobby**. `Project` would give it a Start button in the launcher itself. |
| `configName` | Passed to the game as `--config=`. Defaults to `id` when empty. |
| `manifestFolderUrl` | Without it the game can never be downloaded — only detected if already on disk. |
| `exeName` | Optional. When empty the launcher picks the newest exe that has a sibling `_Data` folder. |

> **If you edit the C# initialiser rather than the inspector**, remember the scene has the
> catalog **serialized into it**, and the serialized copy wins at runtime. Either edit it in
> the inspector, or update the `Catalog:` block in `Assets/Scenes/Launcher.unity` to match.
> `kind:` is written as an integer there — `0` = Project, `1` = Addon, `2` = Game.

Nothing else is needed. `PublishGamesManifest()` picks up every `Game` entry automatically.

---

## 3. In the Lobby — code

Add the game to the `SceneName` enum in
`Assets/_DEV/LobbyAndBotSelection/Utility/SceneNameHolder.cs`:

```csharp
public enum SceneName
{
    RoboRageArena,
    EndlessTunnel,
    EndlessZigZag,
    YourNewGame,      // ← add at the END
}
```

> **Add new members at the end, never in the middle.** Unity serializes enums by ordinal, so
> inserting a member silently re-points every `SceneName` already saved in a scene or prefab.

Also add it to `SceneNameHolder.GetSceneName()` for completeness. For a separately-built game
the scene name is never used — `GamesHandler` checks the manifest first — but leaving the
switch incomplete makes it return `string.Empty`, which is a confusing failure if the game is
ever brought back in-project.

Then **declare it as external** on the `GamesHandler` component in `LOBBY.unity`, by adding the
new `SceneName` to its **External Games** list.

> **Do not skip this.** The manifest tells the lobby *how* a game is doing, but not *what kind*
> of game it is. If the manifest is missing — the lobby opened directly, the Editor, or the
> launcher has not published this game yet — an undeclared game falls through to
> `loader.LoadScene(sceneName)` and fails on a scene that does not exist in this project.
> Externality is a fact about the build; availability is data from the launcher. They are
> tracked separately on purpose.

**That is all the code.** `GamesHandler.ExternalGame()` matches the enum member name against
the manifest, so a game declared external is launched as a process and everything else is
loaded as a scene. No per-game branch to write.

---

## 4. In the Lobby — scene wiring (Editor work)

This part cannot be scripted; it has to be done in the Unity Editor.

On each card in the games section, add a **`GameCardState`** component
(`Assets/_DEV/BackendAPI/EndPoints/Lobby/GameCardState.cs`) and assign:

| Field | What to assign |
|---|---|
| `game` | The `SceneName` member for this card. |
| `configNameOverride` | Leave empty — it comes from the manifest. |
| `readyObj` | The normal "Play" visual. |
| `updateAvailableObj` | Optional. Falls back to `readyObj` when unset. |
| `notInstalledObj` | "Get it in the launcher" visual. |
| `lockedObj` | The padlock visual. |
| `unknownObj` | The "checking…" visual. |

Then point the button's `OnClick` at **`GameCardState.Play`** rather than at `GamesHandler`
directly. `Play()` re-checks the manifest before starting and refuses when the game is not
launchable, which matters because the launcher can republish between the card being drawn and
the player clicking it. There is no `playButton` field — the guard lives in `Play()`, so the
button stays clickable and simply declines.

> Leave `configNameOverride` empty when the id and the backend config name agree, so the
> manifest stays the single source. Set it when they differ: with no manifest — the Lobby opened
> directly, or the Editor — `ResolveConfigName` falls back to the `SceneName` member, which
> would be the wrong config name to send to `/games/start`. That is why the Zig-Zag card sets
> it to `ZigZag`.

`GameCardState` refreshes itself in `OnEnable`, so toggling the games panel picks up anything
installed since the lobby started.

---

## 5. Test checklist

The handshake **cannot be tested in the Editor** — there is no second process to signal it.
Build all three and walk the loop.

| # | Step | Expected |
|---|---|---|
| 1 | Launcher → Start the Lobby | Intro video **plays**. Launcher window disappears only *after* the Lobby is visible. No desktop flash, never two windows. |
| 2 | Open the games section | Each card shows the right state. Uninstalled games are not clickable. |
| 3 | Start the separated game | Lobby shows its loading screen, then the game appears and the Lobby closes. Right robot skin, player still logged in. |
| 4 | Quit / return to lobby | Lobby reappears, intro is **skipped**, player still logged in, games section still populated. |
| 5 | Exit the lobby | Launcher comes back. |
| 6 | Uninstall the game, reopen the Lobby | Card shows "not installed" and refuses to start — and **no energy is consumed**. |

### Troubleshooting

| Symptom | Cause |
|---|---|
| Intro plays when returning from a game | `--launcher=false` not being sent, or an old `LauncherCheck`. |
| Intro skipped on a cold start | The launcher is not sending `--launcher=true` — check for a single-dash argument. |
| Desktop visible during a switch | Something calls `Application.Quit()` after `SwitchTo`, or `HandoffReadySignaller.cs` is missing from the incoming app. |
| Two windows open at once | The incoming app never signalled; the outgoing app is sitting out its 25 s timeout. Check the incoming app actually reached its first scene. |
| Two windows open **forever** (timeout never fires) | The outgoing app's player loop is paused on focus loss. `AppHandoff.SwitchTo` sets `Application.runInBackground = true` to prevent exactly this — check that copy of `AppHandoff.cs` is current. |
| Game closes instantly on launch | The working directory is wrong, so Unity cannot find `<exe>_Data`. Use `ProcessLauncher`, which sets it. |
| Player logged out after a game | `--token=` not being passed back, or `LauncherCheck` gating the token on `--launcher=`. |
| Games section empty after returning | `--gamesManifest=` not handed back by the game. |
| Card always shows "Checking…" | No `games.json`, or the `id` does not match the `SceneName` member. |
| Lobby tries to load a missing scene | The manifest `id` and the `SceneName` member disagree, so the game was treated as in-project. |

---

## 6. Quick reference — what a new game must send and receive

**Receives** (from the Lobby):

```
--token=<jwt>  --config=<name>  --lobbyPath=<lobby exe>  --bot=<Gravy|RN|Spark>
--gamesManifest=<path>  --handshake=<id>
```

**Sends** (back to the Lobby):

```
--launcher=false  --token=<jwt>  --gamesManifest=<path>  --handshake=<id>
```

`--handshake=` is added automatically by `AppHandoff.SwitchTo` on both sides. Never write it
by hand.
