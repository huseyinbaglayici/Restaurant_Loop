# Conveyor System — current state

Snapshot of what's built so far: the level-editor tool, the data model it authors, and the
runtime pipeline that turns a saved level into a running belt, a positioned (but not yet
functional) exit stack and supply queue. Item movement (food riding the belt, or routing in/out
of the exit stack and queue) is **not** built yet.

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
| `ConveyorPrefabSettings.cs` | One persisted asset — which prefabs represent a Straight/Corner piece, and world cell size |
| `LevelDatabase.cs` | One persisted asset — `LevelId -> ConveyorLevelDataSO` lookup table, used to resolve a saved level index back into an asset |
| `FoodItemPrefabSettings.cs` | One persisted asset — the single generic food-stack prefab (all `FoodType`s share it; see `FoodMeshSettings`) |
| `FoodMeshSettings.cs` | One persisted asset — optional `FoodType -> Mesh` lookup; missing entries fall back to `FoodTypeUtil.ToColor` tinting |
| `ExitSlotPrefabSettings.cs` | One persisted asset — the exit-stack slot floor prefab |

All five `*Settings`/`LevelDatabase` assets live under `Assets/Resources/` and are
`Resources.Load`ed once in `PoolInstaller`/`SaveInstaller` (see §6).

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
| `ConveyorGridWindow.cs` | The EditorWindow shell: builds the layout, toolbars and sub-panels around a transient `ConveyorLevelDataSO` draft. |
| `ConveyorGridCanvas.cs` | The main painting canvas: pointer/keyboard input and view only. **Path** (Straight/Corner, Shift+Click or hover+R to rotate, Ctrl+Click toggles Gate), **Entity** and **Customer** modes. |
| `ConveyorLevelEdits.cs` | Every undoable edit to the draft (paint/erase/rotate/gate/entities/customers, resize, reset). All go through `Undo.RecordObject`, which is what makes Ctrl+Z work. |
| `QueueLaneEdits.cs` | Queue lane "Generate From Customers" and "Shuffle Queue". |
| `ConveyorLevelAssetIO.cs` | Save/Load: bakes the path (§4), writes `Assets/Data/Levels/ConveyorLevel_{id}.asset` and registers it in `LevelDatabase`. |
| `GridPainter.cs` / `ConveyorTilePainter.cs` | Painter2D drawing shared by both canvases / conveyor-specific tiles, arrows, customers and gate. |
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

## 5. Runtime conveyor pieces, food, exit stack, queue (`Runtime/Conveyor/`)

All three pooled visual types (piece, food, exit slot) follow the same shape: a `MonoBehaviour`
implementing `IPoolable<...>`
(pure visual, no game logic) + a scene-scoped spawner that's `IInitializable` (spawns on
GameplayScene boot) and `IDisposable` (despawns everything it spawned when GameplayScene
unloads — the pools themselves live in ProjectContext/DDOL and outlive the scene, so nothing
else would ever return these instances otherwise).

| File | Role |
|---|---|
| `ConveyorPieceController.cs` | Pooled visual for one placed belt piece (Straight or Corner). Only owns its belt-scroll texture animation. `OnSpawned` only ever sets Y rotation — X/Z tilt is whatever the prefab was authored with, captured once in `Awake()`. |
| `ConveyorWorldUtil.cs` | The one place grid-coord → world-position/rotation math lives, shared by the editor preview and the runtime spawner so they can't drift apart. |
| `ConveyorPieceSpawner.cs` | Walks the level's `ConveyorCells` (skips the Gate), spawns from the Straight/Corner pools. |
| `FoodItemController.cs` | Pooled visual for one food stack (`FoodStackData` — a queue slot or belt cell). Shows `Amount` as that many stacked unit visuals (plain instantiate/destroy children, not individually pooled), tinted by `FoodTypeUtil.ToColor`; swaps each unit's mesh via `FoodMeshSettings` if an entry exists for that `FoodType` (optional — falls back to color-only). |
| `ExitSlotController.cs` | Pooled visual for one exit-stack slot floor. Position-only. |
| `ExitStackAnchor.cs` | Computes 5 fixed slot positions (`SlotCount`) from the belt's actual min/max X and minimum-Y row — not level-authored, not the declared `GridWidth`. Spawns/despawns floors from the `ExitSlotController` pool. Where food exiting the belt will eventually land, in order — not built yet. |
| `QueueAnchor.cs` | Computes one X position per supply-queue lane (lane count = `Queues.Count`), aligned to `ExitStackAnchor`'s own leftmost/rightmost slot, offset further -Z below it. No spawning itself. |
| `QueueSpawner.cs` | Spawns each lane's `FoodStackData` entries (front = index 0) from the `FoodItemController` pool, one behind another going -Z into the lane. |

## 6. DI / boot wiring (`Runtime/Installers/`, `Runtime/SceneFlow/`)

Two container scopes matter:

- **`ProjectContext`** (`Assets/Resources/ProjectContext.prefab`) — auto-loads once, before any
  scene. Everything here lives for the whole app session.
- **`GameplayScene`'s `SceneContext`** — only exists while GameplayScene is loaded.

| Installer | Scope | Binds |
|---|---|---|
| `PoolInstaller` | ProjectContext | Loads `ConveyorPrefabSettings`/`FoodItemPrefabSettings`/`FoodMeshSettings`/`ExitSlotPrefabSettings` from Resources, prewarms all four `MemoryPool`s (`WithInitialSize` + `NonLazy`) |
| `SceneLoaderInstaller` | ProjectContext | `CurrentLevelProvider`, `SceneLoader` |
| `SignalsInstaller` | ProjectContext | `SignalBus` + all declared signals |
| `SaveInstaller` | ProjectContext | Loads `LevelDatabase` from Resources, binds `SaveManager` |
| `ConveyorInstaller` | **GameplayScene SceneContext** | The current level (from `CurrentLevelProvider`), `ConveyorPieceSpawner`, `ExitStackAnchor`, `QueueAnchor`, `QueueSpawner` |

Everything except `ConveyorInstaller` is project-wide and boots before any scene-specific logic
runs — this is deliberate: pools prewarm once at app start, `ConveyorInstaller` (per GameplayScene
load) just pulls from the already-warm pools according to whatever level is current.

### Boot sequence (as actually wired right now)

1. `ProjectContext` auto-loads → `PoolInstaller` prewarms all pools. `SaveInstaller` binds
   `SaveManager`, which (via eager `IInitializable` resolution) transitively constructs
   `CurrentLevelProvider` — its constructor resolves the current level from ES3 + `LevelDatabase`
   right there (see §7), not in `Initialize()`. This ordering is load-bearing: Zenject calls
   `Initialize()` during Unity's `Start()`, which is always *after* every scene's own
   `SceneContext` has finished `InstallBindings()` in `Awake()` — a boot-time value that only
   existed by `Initialize()` would already be too late for `ConveyorInstaller` to read.
2. `Bootstrapper.Start()` (in `BoostrapScene`) loads `MainMenuScene` additively.
3. `LoadingController.Start()` (in MainMenuScene) calls `ISceneLoader.LoadGameplayScene()`
   **directly** — not via a signal.
4. `SceneLoader.LoadGameplayScene()` fires `LoadingStartedSignal`, loads `GameplayScene`
   additively, sets it active, fires `GameplaySceneReadySignal` on completion.
5. `GameplayScene`'s `SceneContext` builds → `ConveyorInstaller` reads `CurrentLevelProvider.Current`
   (already populated in step 1) and binds the spawners, which pull from the already-warm pools.
6. On `LevelEndedSignal`, `SceneLoader` unloads `GameplayScene` → its `SceneContext`'s container is
   disposed → every spawner's `Dispose()` runs, returning what it spawned to the pool (or, for
   `ExitStackAnchor`'s floors, likewise despawning to its pool) before the scene's own GameObjects
   are torn down.

**Dead code right now**: `PlayRequestedSignal` and `SceneLoader.OnPlayRequested` exist and are
wired up, but nothing fires `PlayRequestedSignal` — `LoadingController` calls
`LoadGameplayScene()` directly instead. This was left in place on purpose as the hook point for
a real "Play"/level-select button later (`PlayRequestedSignal.Level`, when non-null, overrides
whatever `CurrentLevelProvider` already has) — but as of now it's unreachable. Worth deciding
whether to wire a real button to it or delete it once the menu exists.

## 7. Save / level-select (`Runtime/Save/`, `Runtime/Signals/SaveSignals.cs`)

Uses Easy Save 3.

- `CurrentLevelProvider` (ProjectContext singleton) owns the actual ES3 + `LevelDatabase` lookup
  in `ResolveFromSave()` — reads ES3 key `CurrentLevelId` (int), defaults to `1` if unset, looks
  it up in `LevelDatabase`, sets `Current`. Runs once from its own constructor (the boot-time
  default) and again whenever `SaveManager.Load()` is called.
- `SaveManager` (ProjectContext singleton): `Initialize()` subscribes to
  `SaveRequestedSignal`/`LoadRequestedSignal`. `Save()` is an **empty stub** — no save data model
  exists yet beyond the level index. `SetCurrentLevel(int levelId)` writes the ES3 key; nothing
  calls this yet — future hook for "level completed, advance to next" or a level-select menu.

`LevelDatabase.asset` currently has exactly one entry: `ConveyorLevel_1` (LevelId `1`). Adding a
new level = save it from the tool, then drag it into `LevelDatabase`'s list in the Inspector.

## 8. What's explicitly NOT built yet

- Anything that moves food along `BakedPath.OrderedPath` at runtime (a `ConveyorManager` or
  similar) — items are placed by the editor tool but nothing animates them along the belt.
- Anything that routes food *into* the exit stack slots or *out of* the queue lanes — both are
  positioned and can hold spawned stacks, but nothing currently triggers either.
- The level-end win condition (exit stack full, in order).
- A real MainMenu "Play"/level-select UI — `LoadingController` currently loads GameplayScene
  unconditionally on menu start.
- Table gameplay logic (serving orders) — only the data model and editor authoring exist.
- Empirical verification of `ConveyorPathBaker`'s clockwise-direction logic on a real level.
