using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Inserts a new control point into a path_sweep. One per segment, sitting at the segment's chord
/// midpoint; dragging it (in the X/Z plane, grid-snapped) inserts a point there and moves it. A plain
/// click does nothing (insertion only happens once the drag passes the deadzone, so <see cref="Changed"/>
/// stays false). Commits one <see cref="EditPathCommand"/>.
/// </summary>
public sealed class PathInsertHandle : IEditHandle, IStyledHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly int _segIndex; // inserts between _segIndex and _segIndex+1
    private readonly Vector3 _worldOffset;
    private readonly float _cell;
    private readonly Array<Vector3> _orig;
    private readonly Array<float> _origBanks;
    private readonly Vector3 _midLocal;
    private readonly float _midBank;
    private bool _inserted;

    public PathInsertHandle(PrimitiveInstanceData inst, int segIndex, Vector3 worldOffset, float cell)
    {
        _inst = inst;
        _segIndex = segIndex;
        _worldOffset = worldOffset;
        _cell = cell;
        _orig = PathPoints.Read(inst);
        _origBanks = PathPoints.ReadBanks(inst, _orig.Count);
        // Next index wraps so the CLOSING segment (last→first, segIndex = count-1) works: its midpoint is
        // between the last and first points, and Insert(count, …) appends the new point at the seam.
        int next = (segIndex + 1) % _orig.Count;
        _midLocal = (_orig[segIndex] + _orig[next]) * 0.5f;
        _midBank = (_origBanks[segIndex] + _origBanks[next]) * 0.5f;
    }

    public Color WidgetColor => new(1.0f, 0.85f, 0.2f);
    public float WidgetScale => 0.7f;

    public Vector3 Anchor => _worldOffset + _midLocal;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _midLocal, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Mathf.Round((grabNow.X - grabStart.X) / _cell) * _cell;
        float dz = Mathf.Round((grabNow.Z - grabStart.Z) / _cell) * _cell;
        var p = new Vector3(_midLocal.X + dx, _midLocal.Y, _midLocal.Z + dz);

        Array<Vector3> pts = _orig.Duplicate();
        pts.Insert(_segIndex + 1, p);
        Array<float> banks = _origBanks.Duplicate();
        banks.Insert(_segIndex + 1, _midBank);
        _inst.Parameters["points"] = pts;
        _inst.Parameters["banks"] = banks; // keep banks length-aligned during the live preview too
        _inserted = true;
    }

    public void Cancel()
    {
        _inst.Parameters["points"] = _orig.Duplicate();
        _inst.Parameters["banks"] = _origBanks.Duplicate();
        _inserted = false;
    }

    public bool Changed => _inserted;

    public ICommand Commit(Action refresh)
        => new EditPathCommand(_inst, _orig, _origBanks,
            PathPoints.Read(_inst), PathPoints.ReadBanks(_inst, PathPoints.Read(_inst).Count), refresh);
}
