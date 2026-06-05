using System;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Tools;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Moves an opening within the plane of its wall — horizontally along the wall (Offset) and
/// vertically (SillHeight). Grabbed via the opening's placeholder/pick box (no widget). The cursor
/// is projected onto the wall mid-plane; the opening tracks the cursor delta, snapped + clamped +
/// overlap-rejected. Width and height are unchanged.
/// </summary>
public sealed class OpeningMoveHandle : IEditHandle
{
    private const float Step = 0.25f;
    private const float Eps = 1e-3f;

    private readonly PrimitiveInstanceData _wall;
    private readonly OpeningData _opening;
    private readonly OpeningState _orig;
    private readonly float _length;
    private readonly float _wallHeight;
    private readonly Transform3D _wallWorld;
    private float _offset;
    private float _sill;

    public OpeningMoveHandle(PrimitiveInstanceData wall, OpeningData opening, Vector3 elevationOffset)
    {
        _wall = wall;
        _opening = opening;
        _orig = OpeningState.From(opening);
        _offset = _orig.Offset;
        _sill = _orig.Sill;
        _length = GetF(wall, "length", 1f);
        _wallHeight = GetF(wall, "height", 3f);
        _wallWorld = new Transform3D(wall.LocalTransform.Basis, wall.LocalTransform.Origin + elevationOffset);
    }

    public Vector3 Anchor => Vector3.Zero; // unused (no widget)

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _wallWorld.Origin, _wallWorld.Basis.Z.Normalized(), out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        Vector3 start = _wallWorld.AffineInverse() * grabStart;
        Vector3 now = _wallWorld.AffineInverse() * grabNow;

        // Candidate position: horizontal centre (snapped + clamped) and vertical sill (snapped + clamped).
        float origCentreLocalX = _orig.Offset + _orig.Width * 0.5f - _length * 0.5f;
        float desiredCentreLocalX = origCentreLocalX + (now.X - start.X);
        float offsetCand = OpeningPlacement.SnapOffset(_wall, desiredCentreLocalX, _orig.Width, out float snapped) ? snapped : _offset;

        float dy = Mathf.Round((now.Y - start.Y) / Step) * Step;
        float sillCand = Mathf.Clamp(_orig.Sill + dy, 0f, Mathf.Max(0f, _wallHeight - _orig.Height));

        // Apply only if the whole rectangle clears other openings (2D, so stacked neighbours are fine).
        if (!OpeningPlacement.Overlaps(_wall, offsetCand, _orig.Width, sillCand, _orig.Height, _opening.Id))
        {
            _offset = offsetCand;
            _sill = sillCand;
        }

        _opening.Offset = _offset;
        _opening.SillHeight = _sill;
    }

    public void Cancel() => _orig.ApplyTo(_opening);

    public bool Changed => Mathf.Abs(_offset - _orig.Offset) > Eps || Mathf.Abs(_sill - _orig.Sill) > Eps;

    public ICommand Commit(Action refresh)
        => new EditOpeningCommand(_opening, _orig, new OpeningState(_offset, _orig.Width, _orig.Height, _sill), refresh);

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
