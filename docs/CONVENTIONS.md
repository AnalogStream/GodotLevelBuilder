# Conventions

Godot 4.6 / C# (`net10.0`, `Godot.NET.Sdk/4.6.3`).

## C#

- **Namespaces mirror folders** under `LevelBuilder` (e.g. `Source/Core/Primitives` → `LevelBuilder.Core.Primitives`).
- PascalCase for types, methods, properties, public fields, constants. `_camelCase` for private fields. `camelCase` for locals/params.
- One top-level type per file; filename = type name.
- Prefer `readonly`/immutability where practical; expose data via properties.
- Node-attached scripts are `public partial class X : <GodotType>`. Pure-logic classes (geometry, registry, baker) are **plain C#** with no Godot node dependency where possible — keep `Core` headless.
- Nullable reference types: enable and respect (`<Nullable>enable</Nullable>` in csproj when convenient).

## Godot data classes

- Serializable resources: `[GlobalClass] public partial class X : Resource`, parameterless ctor, persisted members `[Export]`.
- Use `Godot.Collections.Array<T>` / `Godot.Collections.Dictionary`, **not** `System.Collections.Generic` containers, for anything serialized or crossing the engine boundary. (Use `System.Collections.Generic` freely for transient in-memory logic.)
- Store geometry/placement as Variant-native types (`Transform3D`, `Vector3`, `float`, …).
- See `DATA_MODEL.md` for the full serialization rules — they are easy to get subtly wrong.

## Geometry

- Build with `SurfaceTool` / `ArrayMesh`. One surface per material slot, stable order.
- CCW winding, outward normals; call `GenerateNormals()` then `GenerateTangents()`.
- 1 unit = 1 metre. UVs in metres so tiling materials are consistent.
- `BuildMesh` / `BuildCollision` must be **pure and deterministic** (same input → same output) — they run for both preview and bake, and determinism underpins stable baking.

## Mutations & undo

- All level-state changes go through `Editor/Commands` (`ICommand` do/undo). Tools/UI never mutate `*Data` directly.
- Commands record touched instance IDs so the viewport invalidates minimally.
- Identify things by **stable string Id**, never by list index.

## Naming the domain

Use the glossary from `CLAUDE.md`: **Level** (document), **Storey** (vertical layer), **Primitive** (type), **primitive instance** (placement), **material slot**, **opening**. Don't reintroduce the ambiguous bare word "level" for a storey.

## Files & assets

- Scenes in `Scenes/`, code under `Source/`, sample assets under `Assets/`. (Migrate the template's `Models/Main.cs` when real work starts.)
- Resource files: `snake_case` is fine for `.tres`/`.tscn` to match Godot defaults; C# files are `PascalCase.cs`.

## Commits

- Small, focused commits. Mention the milestone (`M1:`, `M2:` …) where useful.
- Call out any `SchemaVersion` bump and its migration in the commit body.
