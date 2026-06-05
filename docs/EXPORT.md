# Save, Load & Bake

A level has two on-disk forms:

| Artifact | Format | Purpose | Lifecycle |
|----------|--------|---------|-----------|
| **Source** | `.tres` (`LevelDocument`) | Editable; the authoritative state | Saved/opened in the builder |
| **Baked scene** | `.tscn` (`PackedScene`) | Game-ready geometry + collision | Generated from source; dropped into the game project |

The source is never derived from the bake. The bake is a pure function of the source.

## Save / load

- **Save:** `ResourceSaver.Save(document, path)` → `.tres`. The whole graph (storeys, instances, openings, material library) serializes inline (see `DATA_MODEL.md` for the C# rules). Lossless and re-openable.
- **Load:** `ResourceLoader.Load<LevelDocument>(path)`, then run schema migrations if `SchemaVersion` is behind.
- Saving is independent of baking — you can save work-in-progress that isn't ready to bake.

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
3. Resolve each material slot → `MaterialLibrary` entry → the actual material `.tres`, and assign it to the **mesh surface**.
4. Emit collision under a `StaticBody3D` per storey.
5. `PackedScene.Pack(root)` → `ResourceSaver.Save(scene, targetPath)`.

### Material rule (load-bearing)

Write the library material onto the **mesh surface** (`ArrayMesh` surface material / `MeshInstance3D.MaterialOverride` is *not* used by the baker). **Leave `surface_material_override` empty.**

- Builder owns the **default** material (on the surface).
- The consuming game owns the **override** (`surface_material_override`), set at the *instance* level in the game scene that instances this `.tscn`.

Two clean layers that don't collide: rebaking refreshes defaults without clobbering the game's overrides. If the baker wrote into the override slot instead, every rebake would fight the consumer. **Verify with a round-trip: bake → instance in a test scene → set an override → rebake → confirm the override survives.**

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

## Export target

The builder exports into a chosen folder (typically `res://levels/` of the game project). Both artifacts may be written: `.tres` kept with the builder's projects, `.tscn` written to the game. The export dialog records the last target per level so re-bake is one click.
