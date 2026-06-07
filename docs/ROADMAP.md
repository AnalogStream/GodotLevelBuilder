# Roadmap

Build order is chosen to **prove the riskiest integrations first** (C# nested-Resource serialization, and the bake pipeline), then add breadth. Don't build ten primitives before the spine works end to end.

> **Status (current):** M1–M4 done and F5-verified. Primitive breadth (M6) partly done — floor, wall,
> ramp, stairs, ramp-plane, stair-plane. Editor UI now exists (native Control nodes, see `UI.md`):
> scene-tree dock, primitive/opening palette, right inspector, and a **texture library with
> drag-drop** (onto the inspector slot or the 3D object) covering most of M5. Openings-as-objects
> (the "Next up" item below) is done. Remaining headline gaps: per-type/default material assignment,
> a full `ParamSpec`-driven inspector, arbitrary floor polygons, and the export override round-trip
> check.

## Milestone 1 — The spine (one primitive, whole pipeline)

Goal: a single Floor goes all the way through, proving serialization + bake before any breadth.

- [ ] `Core/Data` minimal: `LevelDocument`, `StoreyData`, `PrimitiveInstanceData`, `MaterialLibrary` as `[GlobalClass] partial : Resource` with `[Export]` Godot collections.
- [ ] `FloorPrimitive` (rectangle): `BuildMesh` (3 slots) + `BuildCollision` (trimesh).
- [ ] **Round-trip test:** build a document with 1 storey + 1 floor → `ResourceSaver.Save` `.tres` → `ResourceLoader.Load` → assert deep equality. *(This is the gate — if nested resources don't persist, fix it here.)*
- [ ] `SceneBaker`: document → `.tscn` with stable node names + surface materials.
- [ ] Open the baked `.tscn` in Godot, confirm the floor renders with collision.

Exit criteria: `.tres` round-trips losslessly **and** the baked `.tscn` opens correctly. No UI required yet — drive it from a test/bootstrap.

## Milestone 2 — Editor shell

- [ ] `Main` app scene: 3D viewport + `EditorCameraRig` (orbit/pan/zoom).
- [ ] `Core/Grid` + grid renderer at active storey elevation; `Snapper` (cell / sub-cell).
- [ ] `ToolManager` + `SelectTool`; selection set + delete.
- [ ] `CommandStack` with undo/redo; all mutations go through commands.
- [ ] Minimal UI: toolbar, storey selector, save/open/bake buttons.

## Milestone 3 — Drawing floors & walls

- [ ] `FloorDrawTool` (rectangle drag on grid).
- [ ] `WallPrimitive` with the box-decomposition mesh (no openings yet).
- [ ] `WallDrawTool` (segment draw, snaps to grid edges).
- [ ] Live preview: regenerate only touched instances on command apply.
- [ ] `PrimitivePalette` (catalog from `PrimitiveRegistry`) + `Inspector` (params from `ParamSpec`).

## Milestone 4 — Openings & frames

- [ ] `OpeningData` on walls; wall mesh + collision honor N openings (box decomposition).
- [ ] `door_frame` / `window_frame` primitives + place/move/delete lifecycle and the **delete-wall-deletes-frames** rule.
- [ ] Reveals on their own material slot.

## Milestone 5 — Materials

- [x] Texture library (`TexturePalettePanel`) + drag-drop assign onto the 3D object or the inspector's Texture slot; runtime-built materials from raw textures (`MaterialEntry.TexturePath`). Whole-object paint, undoable. See `UI.md`.
- [x] **User-added textures** — "Add texture…" copies an image into `res://Assets/user_textures/` (stable `res://` path); `TextureLoader` raw-decode fallback shows/applies it the same session without an editor reimport. See `UI.md`; bake caveat in `EXPORT.md`.
- [x] Baker resolves slots → surface materials (shared `MaterialResolver`, preview == bake).
- [ ] Per-slot assignment + **set-for-type** (apply to all existing instances of a type AND a persisted per-type default in `LevelDocument`).
- [ ] **Override survival round-trip verified** (`EXPORT.md`) — bake a textured object, reopen the `.tscn`, confirm the texture is on the mesh surface with `surface_material_override` empty.

## Milestone 6 — Primitive breadth

- [ ] `stairs`, `ramp`, `column`, `gutter`, `rounded_corner`.
- [ ] Document each primitive's params, slots, UV intent in `PRIMITIVES.md`.
- [ ] "Add a custom primitive" verified by a third-party-style example.

## Milestone 7 — Polish

- [ ] Multi-storey workflow (copy storey, elevation editing, show/hide/dim other storeys).
- [ ] Arbitrary floor polygons (`Geometry2D.TriangulatePolygon`).
- [ ] Mesh merging tuning, draw-call/perf pass.
- [ ] Schema migration framework exercised by a real version bump.

## Next up (designed)

- **Set-for-type materials** — apply a texture to all existing instances of a type AND persist a
  per-type default in `LevelDocument` (supersedes the hardcoded `DefaultMaterials.SlotsFor` for new
  draws; round-trips to `.tres`).
- **ParamSpec-driven inspector** — generate editable fields (spinbox/checkbox/enum) per primitive
  from its `ParamSpec`, plus per-slot material assignment.

### Done

- **Openings as editable objects** — doors/windows are selectable, movable, resizable objects
  (solid coloured placeholder while selected; hole "applies" on deselect and on bake/save). Design
  in `docs/PLANNED_OPENINGS_AS_OBJECTS.md`.

## Explicitly deferred (track, don't build early)

- Proper mitred **wall corner joins** (footprint graph) — v1 uses overlapping boxes.
- Curved walls.
- Lighting/nav/occlusion bake.
- In-editor `EditorPlugin` variant.
