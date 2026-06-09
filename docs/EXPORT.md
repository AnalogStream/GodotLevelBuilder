# Save, Load & Bake

A level has two on-disk forms:

| Artifact | Format | Purpose | Lifecycle |
|----------|--------|---------|-----------|
| **Source** | `.tres` (`LevelDocument`) | Editable; the authoritative state | Saved/opened in the builder |
| **Baked scene** | `.tscn` (`PackedScene`) | Game-ready geometry + collision | Generated from source; dropped into the game project |

The source is never derived from the bake. The bake is a pure function of the source.

## The workspace (where editable data lives)

The builder is a **standalone app**, so its writable data can't live under `res://` (read-only once
the app is exported as a binary). Instead the user picks a **workspace folder** once; it holds:

```
<workspace>/
  levels/      *.tres editable level sources  (the "project files")
  textures/    custom textures, copied here on import
```

The pointer to the workspace persists in `user://levelbuilder.cfg` (`AppConfig`, a `ConfigFile` —
`user://` is the one location always writable, even when exported). `Core/Data/Workspace.cs` holds
the resolved root for the session and is the single arbiter of texture-path resolution: a stored
path is `res://…` (bundled Kenney pack), an absolute OS path, or **workspace-relative**
(`textures/foo.png`) — the relative form is what custom textures store, so a level survives the
workspace folder being moved. The last opened level is remembered and reopened on launch.

## Save / load

- **Save:** `ResourceSaver.Save(document, path)` → `.tres` at `<workspace>/levels/<Name>.tres`. The
  whole graph (storeys, instances, openings, material library) serializes inline (see `DATA_MODEL.md`
  for the C# rules). Lossless and re-openable.
- **Load:** `ResourceLoader.Load<LevelDocument>(path, CacheMode.Ignore)`; `EditorContext.OpenLevel`
  swaps it into the running editor (`ReplaceDocument`: re-targets the view, clears undo/selection,
  activates the ground storey). Then run schema migrations if `SchemaVersion` is behind.
- Saving is independent of baking — you can save work-in-progress that isn't ready to bake.

> **Load-bearing (handled):** Save/Open/Export use **absolute OS paths outside the project**. Godot's
> text writer *drops* the `res://` script `ext_resource` lines when a `.tres` is written directly to an
> external path — producing a scriptless, empty resource (a `LevelDocument` reloads as a bare
> `Resource`). `Core/Build/ResourceIo.cs` works around this: it serialises to a `user://` temp (where
> the `res://` refs are written correctly) then byte-copies the file to the destination, and on load
> copies the external file into a `user://` temp first so the `res://` script refs resolve against the
> running project. `LevelSerializer` and `SceneBaker.*ToFile` both route through it.

## Bake → `.tscn`

`SceneBaker` walks the data graph and builds a `PackedScene`:

```
<LevelName> (Node3D)
├─ Storey_<storeyId>  (Node3D, positioned at BaseElevation)
│  ├─ Mesh_<materialId>   (MeshInstance3D)   ← geometry MERGED by material
│  │     mesh = ArrayMesh, surface material = library material (.tres)
│  ├─ Mesh_<materialId2>  (MeshInstance3D)
│  └─ Collision_<storeyId> (StaticBody3D)
│        └─ Shape_<n> (CollisionShape3D, trimesh / convex)
└─ Storey_<storeyId2> ...
```

Steps:
1. For each primitive instance, call `BuildMesh` and `BuildCollision`.
2. **Merge meshes by material** across the storey (group surfaces sharing a material id into one `MeshInstance3D`) to cut draw calls.
3. Resolve each material slot → `MaterialLibrary` entry → the actual material (a loaded `.tres`, or a `StandardMaterial3D` built from the entry's `TexturePath`), and assign it to the **mesh surface**. Resolution goes through `MaterialResolver`, the *same* class the live `LevelView` uses — so the editor preview and the bake are guaranteed identical. Runtime-built texture materials are embedded inline as sub-resources in the `.tscn`, on the surface (override stays free — see the rule below).
4. Emit collision under a `StaticBody3D` per storey.
5. `PackedScene.Pack(root)` → `ResourceSaver.Save(scene, targetPath)`.

### Material rule (load-bearing)

Write the library material onto the **mesh surface** (`ArrayMesh` surface material / `MeshInstance3D.MaterialOverride` is *not* used by the baker). **Leave `surface_material_override` empty.**

- Builder owns the **default** material (on the surface).
- The consuming game owns the **override** (`surface_material_override`), set at the *instance* level in the game scene that instances this `.tscn`.

Two clean layers that don't collide: rebaking refreshes defaults without clobbering the game's overrides. If the baker wrote into the override slot instead, every rebake would fight the consumer. **Verify with a round-trip: bake → instance in a test scene → set an override → rebake → confirm the override survives.**

### User textures: reimport before baking

User-added textures are copied into `res://Assets/user_textures/` (see `DATA_MODEL.md`/`UI.md`) so they
carry a stable `res://` path. But a texture added during a running session has no imported `.ctex` until
the Godot **editor** reimports it (focus the editor once — same step as the Kenney pack). This affects
*how* the bake references it, not *whether* it works:

- **After reimport (recommended):** the bake references a clean `res://` ext_resource. Smaller `.tscn`.
- **Before reimport:** `MaterialResolver` falls back to a raw `Image` decode, producing a pathless
  runtime `ImageTexture` that Godot **embeds inline** in the `.tscn`. Still correct — still on the mesh
  surface, override slot still free — just larger. Self-heals on a re-bake after reimport.

So: **focus the editor once after adding textures, then bake.** No bake-time guard exists or is needed;
the divergence is bloat, not breakage.

### Node-naming rule (load-bearing)

Node names are **derived from stable IDs** (`Storey_<storeyId>`, `Mesh_<materialId>`), never from iteration order or array index. Reason: in Godot, instance-level `surface_material_override` is keyed by **node path**. If a rebake renames or reorders nodes, the consumer's overrides silently rebind to the wrong node — or vanish. Determinism here is a correctness requirement, not a nicety:

- Same source data ⇒ byte-identical node tree (names, order, structure).
- Sort instances/materials by id before emitting, so ordering is stable across runs.
- Merging by material uses the material id as the node key.

### Re-bake semantics

Re-baking **overwrites** the target `.tscn`. Because:
- node names are stable (above), and
- the consumer instances the scene and overrides at the instance level,

the consumer's material overrides, transforms, and added child nodes on the *instance* survive the rebake. Editing the baked `.tscn`'s contents directly (not via instancing) is not supported — those edits are lost on rebake. Document this for whoever consumes the levels.

## Merged "chunk" bake (separate export)

Alongside the per-instance `Bake` above, `SceneBaker.BakeMerged` produces a **single merged
chunk** for assembling maps cheaply — written to `<Name>_merged.tscn` (a *separate* file; it does
not overwrite the per-instance bake). Triggered from the **Project** tab ("Bake Merged Chunk").

```
<LevelName> (Node3D)
├─ Mesh_<materialId>   (MeshInstance3D)   ← ALL geometry across ALL storeys, merged by material
├─ Mesh_<materialId2>  (MeshInstance3D)
└─ Collision (StaticBody3D)
   └─ Trimesh (CollisionShape3D, one ConcavePolygonShape3D over the whole chunk)
```

- **Storeys are flattened** — `BaseElevation` is baked into each instance transform, so there are
  no per-storey nodes. The whole level is one chunk.
- **Merge by material** via `SurfaceTool.AppendFrom(mesh, surface, worldTransform)`, one
  `SurfaceTool` per resolved material id, committed to one `MeshInstance3D` named
  `Mesh_<materialId>` (id sanitized for node-name rules). Material on the surface, override free —
  same rule as above, but keyed **per material**, not per instance. Draw calls ≈ number of
  distinct materials.
- **Do not regenerate normals/tangents** after `AppendFrom` — the primitives use deliberate flat
  per-quad normals; `AppendFrom` already transforms them. Regenerating would smooth-shade the lot.
- **Collision is one precise trimesh** (`ConcavePolygonShape3D` from the merged `GetFaces()`).
  Trimesh (not convex) is required: it preserves the concavity of half-pipes/bowls/domes (the ball
  rolls *inside*) and keeps wall openings as real holes. Cost: tri-count scales with high-segment
  curves — fine for a handful of chunks.

**Trade-off:** merging collapses per-*instance* overrides into per-*material*. Two walls sharing a
texture can no longer be overridden separately in-game. That's inherent to the chunk approach — use
the per-instance `Bake` when you need per-object override granularity.

## Export to the target game project (inline-embedded textures)

`EditorContext.ExportToGame` (Project tab → "Export to Game", enabled once a target is set) writes a
**merged chunk** into the target game project at `<TargetProjectPath>/levels/<Name>.tscn` — an
**absolute OS path outside this project** (`ResourceSaver.Save` to a global path). The target project
root is chosen once and persisted in `AppConfig.TargetProjectPath`.

The key difference from the local preview bakes: export passes `embedTextures: true`, so materials
are made **self-contained** by `MaterialResolver.ResolveEmbedded`:

- Each material is shallow-`Duplicate(false)`d (so the live-preview cache's original is untouched and
  its path-bearing textures stay shareable) and every texture is replaced with a **pathless**
  `PortableCompressedTexture2D` (lossless PNG) built from the decoded image.
- A pathless resource serializes **inline** as a `sub_resource`, so the exported `.tscn` carries its
  textures with it and opens in *any* project — no `res://` path dependency on the builder or the
  game, no texture-file copying, no matching folder structure required.
- **Why `PortableCompressedTexture2D`, not `ImageTexture`:** a raw `ImageTexture` serializes the full
  *uncompressed* bitmap as base64 text — for prototype textures that bloated a scene to ~45 MB.
  `PortableCompressedTexture2D` stores a PNG-compressed blob inline (still self-contained), shrinking
  it by ~100× for flat textures. For even smaller production output, save the scene as a compressed
  binary `.scn` (`ResourceSaver` `Compress` flag) — not done by default to keep the `.tscn` readable.
- This flattens both texture-entry materials **and** proto `.material` files (whose Kenney textures
  would otherwise serialize as broken `res://` ext_resources).

**Verify (not grep-able):** zero `ext_resource path="res://…"` lines for textures proves
self-containment, but a bad `Decompress` can yield a *pathless garbage* image that still passes that
check — the real gate is opening the exported `.tscn` in the target project and **seeing the texture
render**. The baked tree references only built-in Godot node types, so it opens without the builder's
C#.

The two local **Bake** buttons (per-object → `res://Baked/<Name>.tscn`, merged →
`<Name>_merged.tscn`) are unchanged in-project previews and do **not** embed.
