using System;
using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Removes one corner of one hole — a click affordance (red widget offset to the side of the corner).
/// Like the other remove handles, <see cref="Changed"/> is true from construction so a plain grab+release
/// commits. The provider only creates this while the hole has &gt;3 corners, so the hole stays a triangle;
/// dropping a hole entirely is the separate centre delete handle. Commits one <see cref="SetHolesCommand"/>.
/// </summary>
public sealed class PolygonHoleCornerRemoveHandle : IEditHandle, IStyledHandle
{
    private static readonly Vector3 Offset = new(0.45f, 0.25f, 0.45f);

    private readonly PrimitiveInstanceData _inst;
    private readonly int _hole, _corner;
    private readonly Vector3 _worldOffset;
    private readonly List<List<Vector3>> _origHoles;
    private readonly Vector3 _point;

    public PolygonHoleCornerRemoveHandle(PrimitiveInstanceData inst, int hole, int corner, Vector3 worldOffset)
    {
        _inst = inst;
        _hole = hole;
        _corner = corner;
        _worldOffset = worldOffset;
        _origHoles = PolygonHoles.Decode(inst);
        _point = _origHoles[hole][corner];
    }

    public Color WidgetColor => new(1.0f, 0.35f, 0.35f);
    public float WidgetScale => 0.7f;

    public Vector3 Anchor => _worldOffset + _point + Offset;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _point, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow) { }
    public void Cancel() { }
    public bool Changed => true;

    public ICommand Commit(Action refresh)
    {
        List<List<Vector3>> w = PolygonHoleOps.Clone(_origHoles);
        if (_hole < w.Count && _corner < w[_hole].Count) w[_hole].RemoveAt(_corner);
        return PolygonHoleOps.Command(_inst, _origHoles, w, refresh);
    }
}
