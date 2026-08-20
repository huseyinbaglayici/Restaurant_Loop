# Conveyor System — current state

Snapshot of what's built so far: the level-editor tool, the data model it authors, and the
runtime pipeline that turns a saved level into a running belt. Item movement (food riding the
belt) is **not** built yet — this covers everything up to and including piece pooling/spawning.

Written as a reference for renaming/reorganizing later, not as an API doc — read it once, then
go change whatever naming you want; nothing here is a contract.

## 1. Two worlds: Editor tool vs Runtime

The codebase is split into two assemblies with a hard boundary:

- `Assets/Scripts/Editor/LevelEditor/` (namespace `Editor.LevelEditor`) — Editor-only, uses
  `UnityEditor.AssetDatabase` etc. Never shipped in a player build.
- `Assets/Scripts/Runtime/` (namespace `RestaurantLoop.*`) — ships in the player. Cannot
  reference anything under `Editor.LevelEditor`.

Both sides share one thing: `RestaurantLoop.Runtime.Data` (plain data classes/SOs, no
Editor-only APIs). The Editor tool writes that data; Runtime only ever reads it.

**Known inconsistency**: the Editor side's namespace (`Editor.LevelEditor`) doesn't follow the
`RestaurantLoop.*` root the rest of the project uses. That's a leftover from an external tool
repeatedly corrupting the namespace mid-session — `Editor.LevelEditor` is what it settled on
because it compiles cleanly, not because it's the intended final name.

## 2. Data model (`Runtime/Data/`)

| File | Contents |
|---|---|
| `ConveyorGeometry.cs` | `ConveyorTileShape` (Straight/Corner), `ConveyorCellPaint` (in-editor paint value), `ConveyorCellData` (flattened cell for the level asset) |
| `ConveyorLevelDataSO.cs` | `FoodType` enum + `FoodTypeUtil` (color/icon), `FoodStackData`, `ConveyorEntityPlacement`, `TableData`, `QueueLaneData`, `ConveyorPathData`, and the top-level `ConveyorLevelDataSO` asset itself |
| `ConveyorPrefabSettings.cs` | One persisted asset (`Assets/Resources/ConveyorPrefabSettings.asset`) — which prefabs represent a Straight/Corner piece, and world cell size |
| `LevelDatabase.cs` | One persisted asset (`Assets/Resources/LevelDatabase.asset`) — `LevelId -> ConveyorLevelDataSO` lookup table, used to resolve a saved level index back into an asset |

`ConveyorLevelDataSO` is the single per-level asset: grid size, painted conveyor cells, food
entities riding them, tables (each with its own order queue), supply queue lanes, and the
`BakedPath` (see §4).

**Gate**: at most one `ConveyorCellData` per level has `IsGate = true`. It's a logical seam
(the belt's spawn/despawn point), not a physical piece — no mesh is spawned there, by either
the editor preview or the runtime spawner.

## 3. Editor tool (`Editor/LevelEditor/`)

Entry point: **RestaurantLoop → Conveyor Grid Tool** menu, `ConveyorGridWindow`.

| File | Role |
|---|---|
| `ConveyorGridWindow.cs` | The EditorWindow shell. Owns `_draft` (a transient `ConveyorLevelDataSO`), New/Save/Load, grid resize, mode/brush/palette bar, and hosts the three sub-panels below. Save also runs the path baker (§4) and writes to `Assets/Data/Levels/ConveyorLevel_{id}.asset`. |
| `ConveyorGridCanvas.cs` | The main painting canvas. Three modes: **Path** (stamp Straight/Corner, Shift+Click or hover+R to rotate 90°, Ctrl+Click to toggle Gate), **Entity** (stamp food stacks onto belt cells), **Table** (place/select tables on non-belt cells). Every mutation goes through `Undo.RecordObject(Data, ...)` on the SO, never a plain field — that's what makes Ctrl+Z work. |
| `QueueGridCanvas.cs` | Fixed supply-lane grid (always present, separate concept from Tables). Row 0 = front = servable. |
| `TableOrderListView.cs` | Side-panel editor for one selected table's order list. |
| `EntityLabelFactory.cs` | Shared icon+amount-badge overlay factory, used by both grid canvases. |
| `Css.cs` / `LevelEditor.uss` | Shared USS class-name constants + stylesheet for the whole tool's look. |
| `AssetFolderUtil.cs` | One shared `EnsureFolder(path)` helper. |
| `ConveyorPathBaker.cs` | See §4. |
| `ConveyorSceneBuilder.cs` | "Build In Scene" preview — instantiates real prefabs into the open scene from `ConveyorPrefabSettings`, using the same `ConveyorWorldUtil` math the runtime spawner uses, so preview and gameplay never disagree. |
| `ConveyorSettingsProvider.cs` / `LevelEditorSettingsWindow.cs` | Get-or-create + editor UI for `ConveyorPrefabSettings`. |

## 4. Path baking

`ConveyorPathBaker.TryBake` runs once, at Save time (not at runtime). Input: the level's
unordered `ConveyorCells` (each just has Shape + Rotation). Output: `BakedPath.OrderedPath`, a
`List<Vector2Int>` in actual belt-travel order, starting right after the Gate.

Method: derive which two grid edges each cell's belt touches from its Shape+Rotation, link
cells whose edges face each other into a neighbour graph, require every cell to have exactly
2 neighbours (single closed loop), walk the loop both directions from the Gate, keep whichever
direction is clockwise on screen (negative shoelace signed area).

Runtime/gameplay code should only ever read `BakedPath.OrderedPath` — it must never re-derive
connectivity from Shape/Rotation itself.

**Not yet verified**: this was derived mathematically, not yet confirmed against a real
Gate+loop level's actual baked output. Worth a sanity check before anything reads it.

## 5. Runtime conveyor pieces (`Runtime/Conveyor/`)

| File | Role |
|---|---|
| `ConveyorPieceController.cs` | Pooled visual for one placed piece (Straight or Corner). Knows nothing about neighbours/routing/items — only owns its own belt-scroll texture animation (`Update()` advances a UV offset). Implements `IPoolable<Vector3, float>`: `OnSpawned(position, worldYRotation)` only ever sets Y rotation — X/Z tilt is whatever the prefab was authored with, captured once in `Awake()` so the pool never wipes it out. |
| `ConveyorWorldUtil.cs` | The one place grid-coord → world-position/rotation math lives, shared by the editor preview and the runtime spawner so they can't drift apart. |
| `ConveyorPieceSpawner.cs` | `IInitializable`. On `Initialize()`, walks the current level's `ConveyorCells`, skips the Gate, and calls `pool.Spawn(position, rotation)` per cell using the pool bound in `PoolInstaller`. |

## 6. DI / boot wiring (`Runtime/Installers/`, `Runtime/SceneFlow/`)

Two container scopes matter:

- **`ProjectContext`** (`Assets/Resources/ProjectContext.prefab`) — auto-loads once, before any
  scene. Everything here lives for the whole app session.
- **`GameplayScene`'s `SceneContext`** — only exists while GameplayScene is loaded.

| Installer | Scope | Binds |
|---|---|---|
| `PoolInstaller` | ProjectContext | Loads `ConveyorPrefabSettings` from Resources, prewarms the Straight/Corner `MemoryPool`s (`WithInitialSize` + `NonLazy`) |
| `SceneLoaderInstaller` | ProjectContext | `CurrentLevelProvider`, `SceneLoader` |
| `SignalsInstaller` | ProjectContext | `SignalBus` + all declared signals |
| `SaveInstaller` | ProjectContext | Loads `LevelDatabase` from Resources, binds `SaveManager` |
| `ConveyorInstaller` | **GameplayScene SceneContext** | The current level (from `CurrentLevelProvider`), `ConveyorPieceSpawner` |

Everything except `ConveyorInstaller` is project-wide and boots before any scene-specific logic
runs — this is deliberate: piece pools prewarm once at app start, `ConveyorInstaller` (per
GameplayScene load) just pulls from the already-warm pool according to whatever level is
current.

### Boot sequence (as actually wired right now)

1. `ProjectContext` auto-loads → `PoolInstaller` prewarms piece pools, `SaveInstaller`'s
   `SaveManager.Initialize()` resolves the current level (see §7) into `CurrentLevelProvider`.
2. `Bootstrapper.Start()` (in `BoostrapScene`) loads `MainMenuScene` additively.
3. `LoadingController.Start()` (in MainMenuScene) calls `ISceneLoader.LoadGameplayScene()`
   **directly** — not via a signal.
4. `SceneLoader.LoadGameplayScene()` fires `LoadingStartedSignal`, loads `GameplayScene`
   additively, sets it active, fires `GameplaySceneReadySignal` on completion.
5. `GameplayScene`'s `SceneContext` builds → `ConveyorInstaller` reads
   `CurrentLevelProvider.Current` (already populated in step 1) and binds `ConveyorPieceSpawner`,
   which spawns pieces from the pool.

**Dead code right now**: `PlayRequestedSignal` and `SceneLoader.OnPlayRequested` exist and are
wired up, but nothing fires `PlayRequestedSignal` — `LoadingController` calls
`LoadGameplayScene()` directly instead. This was left in place on purpose as the hook point for
a real "Play"/level-select button later (`PlayRequestedSignal.Level`, when non-null, overrides
whatever `CurrentLevelProvider` already has) — but as of now it's unreachable. Worth deciding
whether to wire a real button to it or delete it once the menu exists.

## 7. Save / level-select (`Runtime/Save/`, `Runtime/Signals/SaveSignals.cs`)

Uses Easy Save 3. `SaveManager` (ProjectContext singleton):

- `Initialize()`: subscribes to `SaveRequestedSignal`/`LoadRequestedSignal`, then immediately
  calls `Load()` once so `CurrentLevelProvider` is populated before anything asks for it.
- `Load()`: reads ES3 key `CurrentLevelId` (int); if it doesn't exist, defaults to `1`. Looks
  that id up in `LevelDatabase`, writes the result into `CurrentLevelProvider.Current`.
- `Save()`: **empty stub** — no save data model exists yet beyond the level index.
- `SetCurrentLevel(int levelId)`: writes the ES3 key. Nothing calls this yet — future hook for
  "level completed, advance to next" or a level-select menu.

`LevelDatabase.asset` currently has exactly one entry: `ConveyorLevel_1` (LevelId `1`). Adding a
new level = save it from the tool, then drag it into `LevelDatabase`'s list in the Inspector.

## 8. What's explicitly NOT built yet

- Anything that moves food along `BakedPath.OrderedPath` at runtime (a `ConveyorManager` or
  similar) — items are placed by the editor tool but nothing animates them along the belt.
- Pooling for the belt-riding food items themselves (separate from piece pooling, deliberately
  deferred until after pieces).
- A real MainMenu "Play"/level-select UI — `LoadingController` currently loads GameplayScene
  unconditionally on menu start.
- Table/queue gameplay logic (serving orders, consuming supply) — only the data model and
  editor authoring exist.
- Empirical verification of `ConveyorPathBaker`'s clockwise-direction logic on a real level.
