using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Moves one control point of a path_sweep. Two per point: a horizontal handle (drag in the X/Z plane,
/// snapped to the grid cell) at the point, and a vertical handle (drag along world up, snapped to the
/// height step) lifted above it — together they give full 3D placement, so a path can climb or loop.
/// The point's other two coordinates stay fixed, so each handle is a clean 1-plane / 1-axis constraint.
/// Edits the instance's "points" array live each frame; commits one <see cref="EditPathCommand"/>.
/// Assumes the instance basis is identity (the draw tool places it so; local == world up to the offset).
/// </summary>
public sealed class PathPointHandle : IEditHandle, IStyledHandle
{
    private const float VertLift = 0.4f; // raise the height widget above the in-plane one so they don't overlap

    private readonly PrimitiveInstanceData _inst;
    private readonly int _index;
    private readonly Vector3 _worldOffset;
    private readonly float _cell, _heightStep;
    private readonly bool _vertical;
    private readonly Array<Vector3> _orig;
    private readonly Array<float> _origBanks;
    private readonly Vector3 _origPoint;
    private Vector3 _point;

    public PathPointHandle(PrimitiveInstanceData inst, int index, Vector3 worldOffset,
        float cell, float heightStep, bool vertical)
    {
        _inst = inst;
        _index = index;
        _worldOffset = worldOffset;
        _cell = cell;
        _heightStep = heightStep;
        _vertical = vertical;
        _orig = PathPoints.Read(inst);
        _origBanks = PathPoints.ReadBanks(inst, _orig.Count);
        _origPoint = _orig[index];
        _point = _origPoint;
    }

    public Color WidgetColor => _vertical ? new Color(0.45f, 1.0f, 0.5f) : new Color(0.3f, 0.7f, 1.0f);
    public float WidgetScale => 1f;

    public Vector3 Anchor => _worldOffset + _point + (_vertical ? new Vector3(0, VertLift, 0) : Vector3.Zero);

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
    {
        Vector3 wp = _worldOffset + _point;
        return _vertical
            ? GizmoMath.ClosestOnAxis(wp, Vector3.Up, rayFrom, rayDir.Normalized(), out world)
            : GizmoMath.RayPlane(rayFrom, rayDir, wp, Vector3.Up, out world);
    }

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        if (_vertical)
        {
            float dy = Snap(grabNow.Y - grabStart.Y, _heightStep);
            _point = new Vector3(_origPoint.X, _origPoint.Y + dy, _origPoint.Z);
        }
        else
        {
            float dx = Snap(grabNow.X - grabStart.X, _cell);
            float dz = Snap(grabNow.Z - grabStart.Z, _cell);
            _point = new Vector3(_origPoint.X + dx, _origPoint.Y, _origPoint.Z + dz);
        }

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

    public ICommand Commit(Action refresh)
        => new EditPathCommand(_inst, _orig, _origBanks, PathPoints.Read(_inst), _origBanks.Duplicate(), refresh);

    private static float Snap(float v, float step) => Mathf.Round(v / step) * step;
}
