using System;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Tools;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Slides an opening along its wall. Grabbed via the opening's placeholder/pick box (no widget).
/// The cursor is projected onto the wall mid-plane; the opening's centre moves by the cursor delta
/// (so the grab point stays under the cursor), snapped + clamped + overlap-rejected like placement.
/// </summary>
public sealed class OpeningSlideHandle : IEditHandle
{
    private const float Eps = 1e-3f;

    private readonly PrimitiveInstanceData _wall;
    private readonly OpeningData _opening;
    private readonly float _origOffset;
    private readonly float _length;
    private readonly Transform3D _wallWorld;
    private float _offset;

    public OpeningSlideHandle(PrimitiveInstanceData wall, OpeningData opening, Vector3 elevationOffset)
    {
        _wall = wall;
        _opening = opening;
        _origOffset = opening.Offset;
        _offset = _origOffset;
        _length = GetF(wall, "length", 1f);
        _wallWorld = new Transform3D(wall.LocalTransform.Basis, wall.LocalTransform.Origin + elevationOffset);
    }

    public Vector3 Anchor => Vector3.Zero; // unused (no widget)

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, _wallWorld.Origin, _wallWorld.Basis.Z.Normalized(), out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float startX = (_wallWorld.AffineInverse() * grabStart).X;
        float nowX = (_wallWorld.AffineInverse() * grabNow).X;
        float origCentreLocalX = _origOffset + _opening.Width * 0.5f - _length * 0.5f;
        float desiredCentreLocalX = origCentreLocalX + (nowX - startX);

        if (!OpeningPlacement.TrySnapOffset(_wall, desiredCentreLocalX, _opening.Width, _opening.Id, out float offset)) return;
        _offset = offset;
        _opening.Offset = offset;
    }

    public void Cancel()
    {
        _opening.Offset = _origOffset;
        _offset = _origOffset;
    }

    public bool Changed => Mathf.Abs(_offset - _origOffset) > Eps;

    public ICommand Commit(Action refresh)
        => new MoveOpeningCommand(_opening, _origOffset, _offset, refresh);

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
