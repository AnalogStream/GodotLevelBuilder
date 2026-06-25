using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Deletes one control point of a path_sweep — a click affordance (red widget lifted above the point's
/// move handles). Unlike the move/insert handles it commits on a plain click: <see cref="Changed"/> is
/// true from construction, so SelectTool's release commits the removal even though no drag (and thus no
/// <see cref="Preview"/>) occurred. The provider only creates these while there are &gt;2 points, so a
/// path can't be reduced below a drawable two. Undoable via <see cref="EditPathCommand"/>.
/// </summary>
public sealed class PathRemoveHandle : IEditHandle, IStyledHandle
{
    // Offset to the SIDE (and slightly up), not stacked on the Y axis above the move/height widgets:
    // in top-down ortho view a Y-stack collapses to one screen pixel, so a select-click could land on
    // remove and silently delete the point. A horizontal offset keeps it a distinct screen target.
    private static readonly Vector3 Offset = new(0.45f, 0.25f, 0.45f);

    private readonly PrimitiveInstanceData _inst;
    private readonly int _index;
    private readonly Vector3 _worldOffset;
    private readonly Array<Vector3> _orig;
    private readonly Array<float> _origBanks;
    private readonly Vector3 _point;

    public PathRemoveHandle(PrimitiveInstanceData inst, int index, Vector3 worldOffset)
    {
        _inst = inst;
        _index = index;
        _worldOffset = worldOffset;
        _orig = PathPoints.Read(inst);
        _origBanks = PathPoints.ReadBanks(inst, _orig.Count);
        _point = _orig[index];
    }

    public Color WidgetColor => new(1.0f, 0.35f, 0.35f);
    public float WidgetScale => 0.7f;

    public Vector3 Anchor => _worldOffset + _point + Offset;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _point, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow) { } // click affordance, not a drag
    public void Cancel() { }
    public bool Changed => true; // a grab+release on this widget removes the point

    public ICommand Commit(Action refresh)
    {
        Array<Vector3> toPts = _orig.Duplicate();
        Array<float> toBanks = _origBanks.Duplicate();
        if (_index >= 0 && _index < toPts.Count) { toPts.RemoveAt(_index); toBanks.RemoveAt(_index); }
        return new EditPathCommand(_inst, _orig, _origBanks, toPts, toBanks, refresh);
    }
}
