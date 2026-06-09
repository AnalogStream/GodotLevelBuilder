using System;
using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Moves several selected instances together across the floor plane. Grabbed via any one of their body
/// colliders; one shared cell-snapped X/Z delta is added to each instance's own original origin, so the
/// group keeps its internal arrangement and each member keeps its own elevation (the selection may span
/// storeys — only X/Z move). Commit bundles a per-instance <see cref="MoveInstanceCommand"/> into one
/// undoable <see cref="MacroCommand"/>.
/// </summary>
public sealed class MultiMoveHandle : IEditHandle
{
    private readonly IReadOnlyList<PrimitiveInstanceData> _instances;
    private readonly Transform3D[] _origXforms;
    private readonly float _step;
    private readonly float _planeY;
    private Vector3 _delta;

    public MultiMoveHandle(IReadOnlyList<PrimitiveInstanceData> instances, float cellSize, float planeY)
    {
        _instances = instances;
        _origXforms = new Transform3D[instances.Count];
        for (int i = 0; i < instances.Count; i++) _origXforms[i] = instances[i].LocalTransform;
        _step = cellSize;
        _planeY = planeY;
    }

    public Vector3 Anchor => _origXforms.Length > 0 ? _origXforms[0].Origin : Vector3.Zero; // unused (no widget)

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.RayPlane(rayFrom, rayDir, new Vector3(0, _planeY, 0), Vector3.Up, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float dx = Mathf.Round((grabNow.X - grabStart.X) / _step) * _step;
        float dz = Mathf.Round((grabNow.Z - grabStart.Z) / _step) * _step;
        _delta = new Vector3(dx, 0, dz);

        for (int i = 0; i < _instances.Count; i++)
        {
            Transform3D xf = _origXforms[i];
            xf.Origin = _origXforms[i].Origin + _delta;
            _instances[i].LocalTransform = xf;
        }
    }

    public void Cancel()
    {
        for (int i = 0; i < _instances.Count; i++) _instances[i].LocalTransform = _origXforms[i];
        _delta = Vector3.Zero;
    }

    public bool Changed => !_delta.IsEqualApprox(Vector3.Zero);

    public ICommand Commit(Action refresh)
    {
        var children = new List<ICommand>(_instances.Count);
        for (int i = 0; i < _instances.Count; i++)
            children.Add(new MoveInstanceCommand(_instances[i], _origXforms[i], _instances[i].LocalTransform, () => { }));
        return new MacroCommand($"Move {_instances.Count} objects", children, refresh);
    }
}
