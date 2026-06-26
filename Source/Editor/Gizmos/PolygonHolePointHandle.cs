using System;
using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Moves one corner of one HOLE of a polygon floor in the X/Z plane (grid-snapped). The hole edit goes
/// through the flat holes/holeSizes arrays (lossy Decode/Encode), so — per the hole-edit rule — it works
/// from a copy of the whole hole set captured at construction, never a fresh Decode mid-drag. Commits one
/// <see cref="SetHolesCommand"/>. Assumes identity instance basis.
/// </summary>
public sealed class PolygonHolePointHandle : IEditHandle, IStyledHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly int _hole, _corner;
    private readonly Vector3 _worldOffset;
    private readonly float _cell;
    private readonly List<List<Vector3>> _origHoles;
    private readonly Vector3 _origPoint;
    private Vector3 _point;

    public PolygonHolePointHandle(PrimitiveInstanceData inst, int hole, int corner, Vector3 worldOffset, float cell)
    {
        _inst = inst;
        _hole = hole;
        _corner = corner;
        _worldOffset = worldOffset;
        _cell = cell;
        _origHoles = PolygonHoles.Decode(inst);
        _origPoint = _origHoles[hole][corner];
        _point = _origPoint;
    }

    public Color WidgetColor => new(1.0f, 0.55f, 0.2f); // warm — a hole corner, vs the blue outer mover
    public float WidgetScale => 1f;

    public Vector3 Anchor => _worldOffset + _point;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _point, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Snap(grabNow.X - grabStart.X, _cell);
        float dz = Snap(grabNow.Z - grabStart.Z, _cell);
        _point = new Vector3(_origPoint.X + dx, _origPoint.Y, _origPoint.Z + dz);
        PolygonHoleOps.WriteLive(_inst, Modified());
    }

    public void Cancel() => PolygonHoleOps.WriteLive(_inst, _origHoles);

    public bool Changed => !_point.IsEqualApprox(_origPoint);

    public ICommand Commit(Action refresh) => PolygonHoleOps.Command(_inst, _origHoles, Modified(), refresh);

    private List<List<Vector3>> Modified()
    {
        List<List<Vector3>> w = PolygonHoleOps.Clone(_origHoles);
        w[_hole][_corner] = _point;
        return w;
    }

    private static float Snap(float v, float step) => Mathf.Round(v / step) * step;
}
