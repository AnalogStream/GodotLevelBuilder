using System;
using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Deletes a WHOLE hole — a click affordance shown at the hole's centre whenever that hole is the active
/// sub-selection. The explicit "remove this hole" action (vs the corner-remove which only thins it). Commits
/// one <see cref="SetHolesCommand"/>. No top-down offset is needed: the centre is planar-separated from the
/// corner widgets in X/Z, so it never collapses onto them in the floor-plan view.
/// </summary>
public sealed class PolygonHoleDeleteHandle : IEditHandle, IStyledHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly int _hole;
    private readonly Vector3 _anchor;
    private readonly List<List<Vector3>> _origHoles;
    private readonly Action _onDeleted;

    public PolygonHoleDeleteHandle(PrimitiveInstanceData inst, int hole, Vector3 worldOffset, Action onDeleted)
    {
        _inst = inst;
        _hole = hole;
        _onDeleted = onDeleted;
        _origHoles = PolygonHoles.Decode(inst);
        _anchor = worldOffset + PolygonHoleOps.Centroid(_origHoles[hole]);
    }

    public Color WidgetColor => new(1.0f, 0.2f, 0.2f);
    public float WidgetScale => 0.9f;

    public Vector3 Anchor => _anchor;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _anchor, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow) { }
    public void Cancel() { }
    public bool Changed => true;

    public ICommand Commit(Action refresh)
    {
        // Removing a hole shifts the indices of the holes after it, so the (hole, corner) sub-selection
        // must be dropped — not left to the count-based clamp, which would silently re-point at a neighbour.
        _onDeleted?.Invoke();
        List<List<Vector3>> w = PolygonHoleOps.Clone(_origHoles);
        if (_hole < w.Count) w.RemoveAt(_hole);
        return PolygonHoleOps.Command(_inst, _origHoles, w, refresh);
    }
}
