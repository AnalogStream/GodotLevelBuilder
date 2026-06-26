using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Extends an OPEN path_sweep past one of its endpoints — the affordance for growing a path after it's
/// drawn. One at the first point, one at the last; each sits a cell beyond the endpoint along the path's
/// outgoing tangent. Dragging (X/Z plane, grid-snapped) adds a new control point there (prepended at the
/// start, appended at the end) and moves it. A plain click does nothing — the point is only added once
/// the drag begins, so <see cref="Changed"/> stays false (mid-path adds are a click on the overlay line). Closed
/// paths get none (a loop has no ends; use the per-segment insert handles instead). One
/// <see cref="EditPathCommand"/> per add.
/// </summary>
public sealed class PathExtendHandle : IEditHandle, IStyledHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly Vector3 _worldOffset;
    private readonly float _cell;
    private readonly Array<Vector3> _orig;
    private readonly Array<float> _origBanks;
    private readonly int _insertAt;   // 0 to prepend, count to append
    private readonly Vector3 _newLocal;
    private readonly float _newBank;
    private bool _added;

    public PathExtendHandle(PrimitiveInstanceData inst, bool atStart, Vector3 worldOffset, float cell)
    {
        _inst = inst;
        _worldOffset = worldOffset;
        _cell = cell;
        _orig = PathPoints.Read(inst);
        _origBanks = PathPoints.ReadBanks(inst, _orig.Count);

        int end = atStart ? 0 : _orig.Count - 1;
        int inner = atStart ? Mathf.Min(1, _orig.Count - 1) : Mathf.Max(0, _orig.Count - 2);
        _insertAt = atStart ? 0 : _orig.Count;

        // Step one grid cell beyond the endpoint along the outgoing tangent (endpoint − its neighbour),
        // snapped to the cell so the new point lands on the grid. Degenerate tangents fall back to +X.
        Vector3 endP = _orig[end];
        Vector3 dir = endP - _orig[inner];
        if (dir.LengthSquared() < 1e-6f) dir = new Vector3(1, 0, 0);
        dir = dir.Normalized() * _cell;
        float dx = Mathf.Round(dir.X / _cell) * _cell;
        float dz = Mathf.Round(dir.Z / _cell) * _cell;
        if (dx == 0 && dz == 0) dx = _cell; // keep it off the endpoint so the widget is a distinct target
        _newLocal = new Vector3(endP.X + dx, endP.Y, endP.Z + dz);
        _newBank = _origBanks[end];
    }

    public Color WidgetColor => new(0.3f, 1.0f, 0.85f);
    public float WidgetScale => 0.7f;

    public Vector3 Anchor => _worldOffset + _newLocal;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _newLocal, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Mathf.Round((grabNow.X - grabStart.X) / _cell) * _cell;
        float dz = Mathf.Round((grabNow.Z - grabStart.Z) / _cell) * _cell;
        var p = new Vector3(_newLocal.X + dx, _newLocal.Y, _newLocal.Z + dz);

        Array<Vector3> pts = _orig.Duplicate();
        pts.Insert(_insertAt, p);
        Array<float> banks = _origBanks.Duplicate();
        banks.Insert(_insertAt, _newBank);
        _inst.Parameters["points"] = pts;
        _inst.Parameters["banks"] = banks; // keep banks length-aligned during the live preview too
        _added = true;
    }

    public void Cancel()
    {
        _inst.Parameters["points"] = _orig.Duplicate();
        _inst.Parameters["banks"] = _origBanks.Duplicate();
        _added = false;
    }

    public bool Changed => _added;

    public ICommand Commit(Action refresh)
        => new EditPathCommand(_inst, _orig, _origBanks,
            PathPoints.Read(_inst), PathPoints.ReadBanks(_inst, PathPoints.Read(_inst).Count), refresh);
}
