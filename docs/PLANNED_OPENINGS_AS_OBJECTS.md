# Planned: openings as editable objects

**Status:** designed, not implemented. Picks up after the current M4 (openings exist as `OpeningData` on a wall and are cut immediately on click).

## Goal

Doors and windows become **first-class, selectable, movable, resizable objects** bound to a wall — not holes committed the instant you click. While you're working on one it shows as a **solid coloured placeholder**; the actual hole only "applies" once you deselect, and on bake/save.

## Behaviour

| State | Wall | Opening |
|-------|------|---------|
| **Selected** | drawn *intact* (that opening's hole suppressed) | solid coloured block (e.g. orange), with move/resize handles |
| **Deselected** | shows the real hole (current behaviour) | invisible (it's a hole) |
| **Bake / save** | real hole always (+ frame trim later) | — edit-time suppression never affects output |

So selecting a door "fills it back in" as a grabbable solid; deselecting "cuts" it. This is purely an **editor-layer** change — the data model and bake pipeline stay as they are today.

## Why this shape

- The hole has no surface, so you can't click it. A first-class object needs its own pick proxy + a visible body while editing.
- Keeping `OpeningData` owned by the wall preserves the box-decomposition mesh + trimesh collision + `.tscn` bake we already built. We only add editing UX on top.

## Implementation sketch

1. **Selection target abstraction.** Selection today is a single `instanceId` string. Generalise to address either an instance *or* an opening (`wallId` + `openingId`). Touch: `EditorContext` selection state, `LevelView` highlight.
2. **Opening pick bodies.** In `LevelView.Rebuild`, emit a box collider at each opening's volume (`StaticBody3D`/`Area3D`) tagged with `{wallId, openingId}`, so the hole is clickable. `InstancePicker`/`PickResult` already return metadata — extend the meta to carry both ids.
3. **Edit-time hole suppression.** `LevelView.Rebuild` passes a "suppressed opening ids" set (the selected opening) into the wall build, so that opening's hole isn't cut; draw a solid coloured placeholder box in its place. Add an optional skip-set param to `WallPrimitive.BuildMesh`, or have `LevelView` build the wall from a filtered copy of `Openings`. **Bake/save must ignore the suppression** (they build the full document).
4. **Move** — drag the selected opening along its wall → `MoveOpeningCommand` (updates `Offset`, snapped, clamped, overlap-rejected — reuse `OpeningTool`'s checks). Dragging onto a different wall **rebinds** to it (the frame↔wall lifecycle in `PRIMITIVES.md`: update owner wall + local offset).
5. **Resize** — handles for `Width` / `Height` / `SillHeight` → `ResizeOpeningCommand`.
6. **Delete** — deleting a wall already deletes its openings (rule in `PRIMITIVES.md`); add delete-opening for a selected opening.
7. **Later: frame trim.** Real door/window moulding geometry via `OpeningData.FrameType` — separate feature; the placeholder block is not the frame.

## Notes / gotchas

- The current `OpeningTool` commits an `OpeningData` and the wall cuts immediately. This redesign replaces "commit = cut" with "commit = add object (shown solid while selected)". The `AddOpeningCommand` stays; what changes is the **view** (suppress hole when selected) and adding move/resize/select for openings.
- Keep the WYSIWYG/overlap rejection already in `OpeningTool` when moving/resizing.
- Gizmo handles (resize) are the most new-work; a first pass can do **move only** (drag along wall) and keep resize via numeric defaults, then add handles.
