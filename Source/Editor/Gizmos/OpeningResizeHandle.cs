using System;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Tools;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>Which edge of an opening a resize handle drives.</summary>
public enum OpeningEdge
{
    WidthMax, // right jamb: grow width, left jamb fixed
    WidthMin, // left jamb:  grow width + shift offset, right jamb fixed
    HeightMax, // top: grow height, sill fixed
    HeightMin, // bottom: grow height + lower sill, top fixed
}

/// <summary>
/// Resizes an opening by dragging one of its four edges along an axis (snapped). The opposite edge
/// stays fixed. Each candidate is validated against minimum size, the wall bounds, and overlap with
/// other openings; an invalid frame is rejected (the edge holds its last valid position).
/// </summary>
public sealed class OpeningResizeHandle : IEditHandle
{
    private const float Step = 0.25f;
    private const float MinSize = 0.1f;

    private readonly PrimitiveInstanceData _wall;
    private readonly OpeningData _opening;
    private readonly OpeningState _orig;
    private readonly OpeningEdge _edge;
    private readonly float _length;
    private readonly float _wallHeight;
    private OpeningState _current;

    public Vector3 Anchor { get; }
    public Vector3 Axis { get; } // unit world direction the edge moves outward along

    public OpeningResizeHandle(PrimitiveInstanceData wall, OpeningData opening, OpeningEdge edge,
                               float length, float wallHeight, Vector3 anchor, Vector3 axis)
    {
        _wall = wall;
        _opening = opening;
        _orig = OpeningState.From(opening);
        _current = _orig;
        _edge = edge;
        _length = length;
        _wallHeight = wallHeight;
        Anchor = anchor;
        Axis = axis;
    }

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.ClosestOnAxis(Anchor, Axis, rayFrom, rayDir, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float delta = (grabNow - grabStart).Dot(Axis);     // outward positive
        float d = Mathf.Round(delta / Step) * Step;
        OpeningState c = Resized(d);
        if (!Valid(c)) return;                              // reject; keep last valid
        _current = c;
        c.ApplyTo(_opening);
    }

    private OpeningState Resized(float d) => _edge switch
    {
        OpeningEdge.WidthMax => _orig with { Width = _orig.Width + d },
        OpeningEdge.WidthMin => _orig with { Offset = _orig.Offset - d, Width = _orig.Width + d },
        OpeningEdge.HeightMax => _orig with { Height = _orig.Height + d },
        OpeningEdge.HeightMin => _orig with { Sill = _orig.Sill - d, Height = _orig.Height + d },
        _ => _orig,
    };

    private bool Valid(OpeningState c)
    {
        if (c.Width < MinSize || c.Height < MinSize) return false;
        if (c.Offset < OpeningPlacement.Margin) return false;
        if (c.Offset + c.Width > _length - OpeningPlacement.Margin) return false;
        if (c.Sill < 0f) return false;
        if (c.Sill + c.Height > _wallHeight) return false;
        return !OpeningPlacement.Overlaps(_wall, c.Offset, c.Width, _opening.Id);
    }

    public void Cancel()
    {
        _orig.ApplyTo(_opening);
        _current = _orig;
    }

    public bool Changed => !_current.Equals(_orig);

    public ICommand Commit(Action refresh) => new EditOpeningCommand(_opening, _orig, _current, refresh);
}
