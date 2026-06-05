# Primitives

A **primitive** is a parametric building block. Each primitive type is one class implementing the primitive contract and registered in the `PrimitiveRegistry`. Placing a primitive creates a `PrimitiveInstanceData` (see `DATA_MODEL.md`). Adding a new primitive = new class + registration; **no data-model changes**.

## The contract

```csharp
public interface IPrimitive
{
    string   TypeId        { get; }   // "wall", "floor", "stairs", ...
    string   DisplayName   { get; }
    string   Category      { get; }   // palette grouping: Structure, Trim, Vertical...

    // Parameter schema drives the inspector UI and supplies defaults.
    IReadOnlyList<ParamSpec> Parameters { get; }   // key, type, default, min/max, label

    // Named surfaces → one material each. Order is stable (drives surface index).
    IReadOnlyList<string> MaterialSlots { get; }    // e.g. ["Front","Back","Reveal"]

    // Build a multi-surface mesh: surface i ↔ MaterialSlots[i]. CCW, normals+tangents.
    ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx);

    // Collision for the baked StaticBody3D. Trimesh for static level geometry.
    Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx);
}
```

`BuildContext` carries storey height, grid cell size, and the `MaterialLibrary` (for slot→material resolution at bake time). `BuildMesh` is called both for live preview and for bake — it must be pure and deterministic.

### Mesh rules
- One **surface per material slot**, in `MaterialSlots` order. Empty surfaces are allowed (skip slot).
- CCW winding, outward normals. Generate normals + tangents (`SurfaceTool.GenerateNormals/Tangents`) so normal-mapped materials work in-game.
- Local space: build around the instance origin; the baker applies `LocalTransform` + storey elevation.
- UVs: world-ish scale (1 unit = 1 metre) so tiling materials look consistent across primitives. Document per-primitive UV intent if it deviates.

## Wall openings — box decomposition (load-bearing)

Door/window holes are **not** made by triangulating a polygon-with-hole. A wall segment is a box of length `L`, height `H`, thickness `T`. Each opening (offset `x`, width `w`, sill `s`, opening height `h`) splits the wall into **solid sub-boxes**:

```
elevation view of a wall with one window opening:

 ┌───────────────────────────────────────┐  ← H (storey/ wall height)
 │            HEADER block                │
 │        (above opening: s+h..H)         │
 ├──────────┬───────────────┬────────────┤  ← s + h
 │          │               │            │
 │  LEFT    │   (opening)    │   RIGHT    │
 │  block   │  reveals only  │   block    │
 │ (0..x)   │               │ (x+w..L)   │
 ├──────────┼───────────────┼────────────┤  ← s
 │          │  SILL block   │            │
 │  LEFT    │  (0..s)       │   RIGHT    │   (doors: s=0, no sill block)
 └──────────┴───────────────┴────────────┘  ← 0
```

- **LEFT** `[0, x]` and **RIGHT** `[x+w, L]` are full-height boxes.
- **SILL** `[x, x+w] × [0, s]` exists only for windows (`s>0`).
- **HEADER** `[x, x+w] × [s+h, H]`.
- **Reveals**: the four inner faces of the opening (left jamb, right jamb, sill top, header bottom) are quads on their own material slot (`Reveal`).

**Multiple openings (incl. stacked):** the front/back faces are swept in two passes. First split the wall into **vertical strips** at every opening edge (the unique `x` and `x+w` values, plus the wall ends). Then within each strip, take the openings that cover it, sort them by sill, and emit a solid quad for each **y-band** in the gaps (below the lowest, between consecutive openings, above the highest); add the top/bottom face for that strip unless an opening reaches the ceiling/floor there. Reveals are emitted per opening. This handles openings side by side **and stacked vertically** (a window above a door, two windows…) with no boolean ops, and for non-stacked walls emits exactly the old slab geometry. Openings may not overlap in 2D (offset×height); the edit tools and `CollectValid` enforce it. Collision uses the trimesh of the same geometry — cheap and exact.

> Never reach for CSG or polygon-hole triangulation here. The decomposition above is the whole feature.

## Frame ↔ wall lifecycle (load-bearing)

A door/window **frame** is trim geometry tied to an opening, which is owned by a wall. Define the lifecycle explicitly:

- **Place:** when a frame tool clicks on a wall face, bind to that wall's **stable `Id`** + a **local offset** along the wall; create the `OpeningData` on that wall (so the hole and the frame appear together). The frame's geometry is generated by the frame primitive from the opening's dimensions.
- **Move:** dragging re-resolves the nearest wall face and **rebinds** (updates owner wall Id + offset). Moving off any wall is either rejected or leaves the frame unbound (decide per tool; default = reject).
- **Resize:** editing opening width/height/sill updates `OpeningData`; both the wall (re-decomposed) and the frame regenerate.
- **Delete wall:** **its openings and their frames are deleted with it** (default rule). Orphaned frames are not allowed. If a "detach frame" affordance is added later, it must clear the binding explicitly.

Openings live on the wall (`PrimitiveInstanceData.Openings`), not as free instances, precisely so this lifecycle has one owner.

## Built-in primitives (initial catalog)

| TypeId | Category | Key params | Material slots |
|--------|----------|------------|----------------|
| `floor` | Structure | width, depth, thickness | Top, Bottom, Edge |
| `wall` | Structure | length, height, thickness, openings | Front, Back, Top, Reveal |
| `door_frame` | Trim | jamb width/depth (from opening) | Frame |
| `window_frame` | Trim | jamb width/depth, mullions (from opening) | Frame, Glass |
| `stairs` | Vertical | steps, totalRise, run, width | Tread, Riser, Side |
| `gutter` | Trim | length, profile, radius | Surface |
| `rounded_corner` | Structure | radius, height, segments, thickness | Outer, Inner, Top |
| `column` | Structure | radius/size, height, sides | Surface, Cap |
| `ramp` | Vertical | length, rise, width | Surface, Side |

This list is a starting point, not a closed set — the registry is the extension point.

## Adding a custom primitive

1. Implement `IPrimitive` (or extend a `PrimitiveBase` helper) in `Core/Primitives`.
2. Declare `Parameters` (drives the inspector) and `MaterialSlots`.
3. Implement `BuildMesh` (multi-surface) and `BuildCollision`.
4. Register it in `PrimitiveRegistry` (one line). It appears in the palette under its `Category`.
5. No data-model, serializer, or baker changes needed.

## Known-hard cases (don't over-build v1)

- **Wall corner joins.** v1: independent wall boxes that overlap slightly at corners (visually fine for most materials). "Proper" mitred joins need a wall-network footprint graph — **defer**.
- **Arbitrary floor shapes.** v1: axis-aligned rectangles. Arbitrary polygons via `Geometry2D.TriangulatePolygon` later.
- **Curved/rounded geometry** (rounded corners, gutters): build by revolve/extrude of a profile with a segment count param; keep segment counts modest for collision.
- **Collision** for static level geometry is **trimesh** (concave) per primitive, or convex sub-boxes where the decomposition gives them (walls). Don't generate per-triangle convex hulls.
