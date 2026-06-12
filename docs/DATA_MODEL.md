# Data Model

The editable source of a level is a graph of Godot `Resource` objects, serialized to a single `.tres`. This is the **only** authoritative state; the viewport mesh and the baked `.tscn` are both derived from it.

## The graph

```
LevelDocument : Resource
├─ string SchemaVersion              // bump on breaking changes; migrate on load
├─ GridSettings Grid                 // cell size, subdivisions, snap mode
├─ MaterialLibrary Materials         // id → material entry
└─ Array<StoreyData> Storeys

StoreyData : Resource
├─ string Id                         // stable, unique within the level
├─ string Name                       // "Ground Floor"
├─ float BaseElevation               // metres
├─ float Height                      // storey height (default wall height)
└─ Array<PrimitiveInstanceData> Instances

PrimitiveInstanceData : Resource
├─ string Id                         // stable, unique within the level
├─ string PrimitiveType              // registry key: "wall", "floor", "stairs", ...
├─ Transform3D LocalTransform        // placement within the storey (pre-snapped)
├─ Dictionary Parameters             // typed primitive params (length, steps, radius…)
├─ Dictionary MaterialSlots          // slotName → material id (into MaterialLibrary)
└─ Array<OpeningData> Openings       // walls only; empty otherwise

OpeningData : Resource              // a hole punched in a wall
├─ string Id
├─ float Offset                      // distance along the wall, metres
├─ float Width
├─ float Height
├─ float SillHeight                  // 0 for doors, >0 for windows
├─ string FrameType                  // "" = bare hole, else a frame primitive type
└─ Dictionary FrameMaterialSlots

MaterialLibrary : Resource
└─ Array<MaterialEntry> Entries
     MaterialEntry { string Id; string DisplayName; string MaterialPath; string TexturePath;
                     float UvScale; Color Tint;            // tiling + tint
                     bool Pixelated; int PixelSize; }      // texture-built materials only
```

`Parameters` and `MaterialSlots` are dictionaries (not typed fields) so new primitives need **no** data-class changes — only a registry entry. Each primitive documents its own parameter keys/types (`PRIMITIVES.md`).

**A material entry is either a file or a raw texture.** `MaterialPath` points at a `Material` resource (`.tres`/`.material`). `TexturePath` points at a raw `Texture2D`; when `MaterialPath` is empty the resolver builds a `StandardMaterial3D` with it as albedo at runtime (cached) — so picking from the texture library needs no on-disk `.material` per texture. `MaterialResolver` (shared by the live view and the baker) does the resolution; `MaterialSlots` still stores only string ids, so the `.tres` round-trip is unaffected. The library is an **append-only, id-deduped pool** (seeded defaults + textures registered on first use via `TextureCatalog.EnsureEntry`); registration lives outside undo, only slot *assignment* is undoable.

**Texture properties are per-entry (shared).** `UvScale` (tiling — a multiplier on the world-unit UVs; higher = more repeats), `Tint` (albedo multiply), and **pixelation** (`Pixelated` + `PixelSize`: downsample the texture to `PixelSize` texels on its longest side and show it with a Nearest filter for a chunky pixel-art look) live on the `MaterialEntry` and are applied by `MaterialResolver` only to texture-built `StandardMaterial3D`s (never to a loaded `.material` — that would mutate a shared resource). Because a texture is one entry referenced by every instance that uses it, editing these from the inspector affects all of them — conventional shared-material semantics. Editing is undoable (`EditMaterialCommand`); the live view's long-lived resolver caches by id, so the edit path **evicts** the cached build (`LevelView.InvalidateMaterial`) before rebuilding. Round-trips via `.tres`; entries from older saves default to `UvScale=1`, `Tint=white`, `Pixelated=false`, `PixelSize=32`.

**Texture paths are always `res://`.** Both the bundled pack and **user-added** textures live under the project: "Add texture…" copies the chosen file into `res://Assets/user_textures/` precisely so `TexturePath` is a stable project-relative path that survives save/bake (an external OS path could not be referenced by a `.tres`/`.tscn`). Loading goes through `TextureLoader.Load`, which prefers Godot's imported `.ctex` but falls back to a raw `Image.LoadFromFile` decode for a texture added this session (no `.ctex` yet) — see `UI.md` for the flow and `EXPORT.md` for the bake-before-reimport caveat.

## IDs

Every `StoreyData`, `PrimitiveInstanceData`, and `OpeningData` carries a **stable string `Id`** (GUID-ish, assigned on creation, never reused). IDs are what:
- frames use to bind to their owning wall,
- the baker uses to derive **deterministic node names** (see `EXPORT.md`),
- the command stack uses to target mutations for undo/redo.

Never identify an instance by list index — indices shift on insert/delete and break bindings.

## Serialization rules & C# gotchas (Godot 4.6)

Nested custom-`Resource` serialization in Godot C# is **the** early risk. Pin these:

1. **Every serializable class is `[GlobalClass] public partial class X : Resource`** with a parameterless ctor. Without `[GlobalClass]` the type won't round-trip cleanly through `.tres`.
1b. **One serializable class per file, filename = class name** (e.g. `MaterialEntry` *must* live in `MaterialEntry.cs`). Godot reconstructs a `[GlobalClass]` C# resource from a `.tres` by matching the class to a file of the same name; bury it in another file and load fails with *"Cannot instantiate C# script because the associated class could not be found"* and the nested array comes back **empty** while sibling data loads fine. (Hit in M1 — `MaterialEntry` was inside `MaterialLibrary.cs`.)
2. **Use typed `Godot.Collections.Array<T>` / `Godot.Collections.Dictionary`,** not `System.Collections.Generic.List<T>`. Only Godot collections of Godot-marshalable types serialize. A `List<PrimitiveInstanceData>` will *not* persist.
3. **Mark persisted members `[Export]`.** Plain public properties are ignored by `ResourceSaver`.
4. **Nested resources save inline by default** when they have no own `.tres` path — exactly what we want (one self-contained level file). Verify they aren't being written as external `ExtResource` references.
5. **`Dictionary` values must be Variant-compatible** (numbers, strings, `Vector*`, bools, nested `Resource`/Array/Dictionary). Don't stuff arbitrary C# objects into `Parameters`.
6. **`Transform3D`, `Vector3`, etc. are Variant-native** — store geometry placement as these, not as custom structs.

> **Milestone 1 must prove this round-trips:** create a `LevelDocument` with one storey + one floor instance, `ResourceSaver.Save` to `.tres`, `ResourceLoader.Load` it back, and assert deep equality. Do this *before* building more primitives — discovering a serialization sharp edge after ten primitives is the failure mode to avoid.

## Versioning & migration

`SchemaVersion` is checked on load. Migrations are forward-only functions keyed by version. Keep old fields readable until a migration drops them. Bumping the schema without a migration path is a breaking change — call it out in the changelog/commit.

## Mutation discipline

`*Data` resources are mutated **only** through `Editor/Commands` (`ICommand` with do/undo). Tools and UI never write fields directly. This keeps undo/redo, dirty-tracking, and viewport invalidation consistent. A command records which instance IDs it touched so the viewport regenerates only those meshes.
