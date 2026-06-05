using System;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Translates an instance across the storey floor plane. Grabbed via the instance's own body
/// collider (no widget). The cursor delta is snapped in whole cells and added to the start origin,
/// so the object keeps its original sub-cell alignment instead of jumping to the cursor.
/// </summary>
public sealed class InstanceMoveHandle : IEditHandle
{
    private readonly PrimitiveInstanceData _inst;
    private readonly Transform3D _origXform;
    private readonly Vector3 _origOrigin;
    private readonly float _step;
    private readonly float _planeY;
    private Vector3 _origin;

    public InstanceMoveHandle(PrimitiveInstanceData inst, float cellSize, float planeY)
    {
        _inst = inst;
        _origXform = inst.LocalTransform;
        _origOrigin = _origXform.Origin;
        _origin = _origOrigin;
        _step = cellSize;
        _planeY = planeY;
    }

    public Vector3 Anchor => _origOrigin; // unused (no widget)

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, new Vector3(0, _planeY, 0), Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Mathf.Round((grabNow.X - grabStart.X) / _step) * _step;
        float dz = Mathf.Round((grabNow.Z - grabStart.Z) / _step) * _step;
        _origin = new Vector3(_origOrigin.X + dx, _origOrigin.Y, _origOrigin.Z + dz);

        Transform3D xf = _origXform;
        xf.Origin = _origin;
        _inst.LocalTransform = xf;
    }

    public void Cancel()
    {
        _inst.LocalTransform = _origXform;
        _origin = _origOrigin;
    }

    public bool Changed => !_origin.IsEqualApprox(_origOrigin);

    public ICommand Commit(Action refresh)
        => new MoveInstanceCommand(_inst, _origXform, _inst.LocalTransform, refresh);
}
