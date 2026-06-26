using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Moves one corner of a polygon floor in the X/Z plane (snapped to the grid cell). The outline is
/// planar, so — unlike <see cref="PathPointHandle"/> — there's no height or bank variant: one clean
/// in-plane drag, the corner's Y left untouched. Edits the instance's "points" array live each frame;
/// commits one <see cref="EditPointsCommand"/>. Assumes the instance basis is identity (the draw tool
/// places it so; local == world up to the offset).
/// </summary>
public sealed class PolygonPointHandle : IEditHandle, IStyledHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly int _index;
    private readonly Vector3 _worldOffset;
    private readonly float _cell;
    private readonly Array<Vector3> _orig;
    private readonly Vector3 _origPoint;
    private Vector3 _point;

    public PolygonPointHandle(PrimitiveInstanceData inst, int index, Vector3 worldOffset, float cell)
    {
        _inst = inst;
        _index = index;
        _worldOffset = worldOffset;
        _cell = cell;
        _orig = PathPoints.Read(inst);
        _origPoint = _orig[index];
        _point = _origPoint;
    }

    public Color WidgetColor => new(0.3f, 0.7f, 1.0f);
    public float WidgetScale => 1f;

    public Vector3 Anchor => _worldOffset + _point;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _point, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Snap(grabNow.X - grabStart.X, _cell);
        float dz = Snap(grabNow.Z - grabStart.Z, _cell);
        _point = new Vector3(_origPoint.X + dx, _origPoint.Y, _origPoint.Z + dz);

        Array<Vector3> arr = _orig.Duplicate();
        arr[_index] = _point;
        _inst.Parameters["points"] = arr;
    }

    public void Cancel()
    {
        _inst.Parameters["points"] = _orig.Duplicate();
        _point = _origPoint;
    }

    public bool Changed => !_point.IsEqualApprox(_origPoint);

    public ICommand Commit(Action refresh) => new EditPointsCommand(_inst, _orig, PathPoints.Read(_inst), refresh);

    private static float Snap(float v, float step) => Mathf.Round(v / step) * step;
}
