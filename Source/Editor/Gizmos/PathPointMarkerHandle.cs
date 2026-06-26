using System;
using Godot;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// A small, click-to-select marker for one UNSELECTED control point of a path_sweep (the selected
/// point shows its full move/height/bank/remove gizmos instead). Clicking it makes that point active
/// — handled by <c>SelectTool</c> via <see cref="IPathPointSelect"/>, so this handle never drags and
/// never commits a command (<see cref="Changed"/> is always false). Endpoints read green (first) /
/// red (last) like Godot's Path3D so the path direction is legible at a glance.
/// </summary>
public sealed class PathPointMarkerHandle : IEditHandle, IStyledHandle, IPathPointSelect
{
    private readonly Vector3 _anchor;
    private readonly Color _color;

    public PathPointMarkerHandle(int index, Vector3 worldAnchor, Color color, int ring = -1)
    {
        Index = index;
        Ring = ring;
        _anchor = worldAnchor;
        _color = color;
    }

    public int Index { get; }
    public int Ring { get; }

    public Color WidgetColor => _color;
    public float WidgetScale => 0.6f;

    public Vector3 Anchor => _anchor;

    // Selection is intercepted before the drag path, so these are never exercised — implemented as inert.
    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world) { world = _anchor; return true; }
    public void Preview(Vector3 grabStart, Vector3 grabNow) { }
    public void Cancel() { }
    public bool Changed => false;
    public ICommand Commit(Action refresh) => throw new NotSupportedException("Markers select, they don't commit.");
}
