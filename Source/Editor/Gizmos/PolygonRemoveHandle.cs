using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Deletes one corner of a polygon floor — a click affordance (red widget offset to the side of the
/// corner, not stacked above it, so it stays a distinct screen target in top-down view). Like
/// <see cref="PathRemoveHandle"/>, <see cref="Changed"/> is true from construction so a plain
/// grab+release commits the removal even though no drag occurred. The provider only creates these while
/// there are &gt;3 corners, so the outline can't drop below a triangle. Undoable via
/// <see cref="EditPointsCommand"/>.
/// </summary>
public sealed class PolygonRemoveHandle : IEditHandle, IStyledHandle
{
    private static readonly Vector3 Offset = new(0.45f, 0.25f, 0.45f);

    private readonly PrimitiveInstanceData _inst;
    private readonly int _index;
    private readonly Vector3 _worldOffset;
    private readonly Array<Vector3> _orig;
    private readonly Vector3 _point;

    public PolygonRemoveHandle(PrimitiveInstanceData inst, int index, Vector3 worldOffset)
    {
        _inst = inst;
        _index = index;
        _worldOffset = worldOffset;
        _orig = PathPoints.Read(inst);
        _point = _orig[index];
    }

    public Color WidgetColor => new(1.0f, 0.35f, 0.35f);
    public float WidgetScale => 0.7f;

    public Vector3 Anchor => _worldOffset + _point + Offset;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _worldOffset + _point, Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow) { } // click affordance, not a drag
    public void Cancel() { }
    public bool Changed => true; // a grab+release on this widget removes the corner

    public ICommand Commit(Action refresh)
    {
        Array<Vector3> toPts = _orig.Duplicate();
        if (_index >= 0 && _index < toPts.Count) toPts.RemoveAt(_index);
        return new EditPointsCommand(_inst, _orig, toPts, refresh);
    }
}
