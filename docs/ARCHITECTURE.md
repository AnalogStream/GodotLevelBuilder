# Architecture

LevelBuilder is a standalone Godot 4.6 / C# app. It edits a **Level** (a building) made of **Storeys** (vertical layers), each holding placed **primitive instances** drawn on a snapping 3D grid. Levels serialize to an editable `.tres` and bake to a game-ready `.tscn`.

## Layers

```
┌──────────────────────────────────────────────────────────────┐
│ UI            Toolbar · Palette · Inspector · StoreySelector   │
│               · MaterialPicker            (Godot Control nodes) │
├──────────────────────────────────────────────────────────────┤
│ Editor        Camera rig · Tool state machine (Select/         │
│               FloorDraw/WallDraw/Place) · Selection · Gizmos    │
│               · Command stack (undo/redo)                       │
├──────────────────────────────────────────────────────────────┤
│ Core          Data model (Resources) · Primitive registry +    │
│               primitive definitions · Geometry builders · Grid  │
│               + snapping · Build/bake pipeline                  │
├──────────────────────────────────────────────────────────────┤
│ Godot 4.6     ArrayMesh · SurfaceTool · ResourceSaver/Loader ·  │
│               PackedScene · StaticBody3D/CollisionShape3D        │
└──────────────────────────────────────────────────────────────┘
```

**Dependency rule:** UI → Editor → Core → Godot. Core never references Editor/UI. This keeps the data model and geometry/bake logic headless and testable, and makes a future "rebuild from data in-game" library extractable from Core.

## Data flow

```
            user input (mouse on grid, palette pick, inspector edit)
                                 │
                          Editor/Tools
                                 │  emits Commands
                          Command stack ── undo/redo ──┐
                                 │ applies                │
                          Core/Data  (LevelDocument …)    │ dirty flag
                                 │                         │
        ┌────────────────────────┼─────────────────────────┐
        │ live preview           │ save                     │ bake
        ▼                        ▼                          ▼
  Primitive.BuildMesh      ResourceSaver → .tres      SceneBaker → .tscn
  → ArrayMesh in viewport   (editable source)         (MeshInstance3D +
                                                       StaticBody3D, by material)
```

- **Editing** is data-first: tools never touch meshes directly. They produce commands that mutate `*Data` resources; the viewport regenerates affected primitive meshes for preview.
- **Saving** writes the data graph verbatim (`.tres`). Re-openable, lossless.
- **Baking** is a pure function of the data graph → `PackedScene`. Deterministic: same data → byte-stable node names (required for material-override stability, see `EXPORT.md`).

## Modules

| Module | Responsibility | Key types |
|--------|----------------|-----------|
| `Core/Data` | The serializable level graph | `LevelDocument`, `StoreyData`, `PrimitiveInstanceData`, `MaterialLibrary`, `OpeningData` |
| `Core/Primitives` | Parametric building blocks | `IPrimitive`/`PrimitiveDefinition`, `PrimitiveRegistry`, concrete primitives |
| `Core/Geometry` | Low-level mesh helpers | `MeshBuilder` (quads/boxes/extrude/revolve), wall box-decomposition |
| `Core/Grid` | Grid math & snapping | `GridModel`, `Snapper` |
| `Core/Build` | Persistence + bake | `LevelSerializer` (.tres), `SceneBaker` (.tscn) |
| `Editor/Camera` | Viewport navigation | `EditorCameraRig` (orbit/pan/zoom) |
| `Editor/Tools` | Interaction modes | `ITool`, `ToolManager`, `SelectTool`, `FloorDrawTool`, `WallDrawTool`, `PlaceTool` |
| `Editor/Commands` | Undo/redo | `ICommand`, `CommandStack` |
| `Editor/Selection` | Selection + gizmos | `SelectionSet`, transform gizmos |
| `UI` | Panels & widgets | `Toolbar`, `PrimitivePalette`, `Inspector`, `StoreySelector`, `MaterialPicker` |

## Key design decisions (and why)

- **Procedural ArrayMesh, not CSG.** Stairs/gutters/rounded corners aren't natural booleans, and CSG merge smears material assignment. Per-primitive generators give full control, an extensible catalog, and clean material slots. (CSG may be used *inside* a single primitive's bake later if some shape is genuinely painful to hand-mesh — but it's not the system.)
- **Data/Editor/UI split.** Lets Core run headless for tests and for a possible runtime rebuild library.
- **Command-stack mutations.** Single source of truth for undo/redo + dirty tracking; tools and UI stay dumb.
- **Two artifacts (.tres + .tscn).** Editable source decoupled from optimized output; rebake never loses the source, and consumer material overrides survive (see `EXPORT.md`).
- **Storeys as the organizing axis.** Multi-floor buildings fall out naturally; the grid renders at the active storey's elevation.

See `DATA_MODEL.md`, `PRIMITIVES.md`, `EXPORT.md` for the load-bearing details, and `ROADMAP.md` for build order.
