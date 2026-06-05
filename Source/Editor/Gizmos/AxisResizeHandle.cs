using System;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Resizes one dimension of an instance by dragging a face handle along its axis. The dragged
/// distance (snapped) grows/shrinks the parameter and the origin translates by
/// <c>shiftFactor · growth · axis</c>. Because the primitive's local geometry is fixed relative to
/// its origin, that shift is what pins the face you want: 0 = origin stays (the origin-side face is
/// fixed, the opposite face moves out); 0.5 = centered (opposite face fixed); 1.0 = origin follows
/// the full growth (e.g. a floor whose top grows upward while its bottom, fixed below the origin in
/// local space, stays put).
/// </summary>
public sealed class AxisResizeHandle : IEditHandle
{
    private const float Step = 0.25f;

    private readonly PrimitiveInstanceData _inst;
    private readonly string _param;
    private readonly float _orig;
    private readonly float _min, _max;
    private readonly Transform3D _origXform;
    private readonly float _shiftFactor;
    private float _current;

    public Vector3 Anchor { get; }
    public Vector3 Axis { get; } // unit world direction the face moves outward along

    public AxisResizeHandle(PrimitiveInstanceData inst, string param, float min, float max,
                            Vector3 anchor, Vector3 axis, float shiftFactor)
    {
        _inst = inst;
        _param = param;
        _min = min;
        _max = max;
        _orig = GetF(inst, param);
        _current = _orig;
        _origXform = inst.LocalTransform;
        _shiftFactor = shiftFactor;
        Anchor = anchor;
        Axis = axis;
    }

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.ClosestOnAxis(Anchor, Axis, rayFrom, rayDir, out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        float delta = (grabNow - grabStart).Dot(Axis);
        float snapped = Mathf.Round(delta / Step) * Step; // snap + jitter-guard: tiny drags resolve to 0
        float val = Mathf.Clamp(_orig + snapped, _min, _max);
        _current = val;

        _inst.Parameters[_param] = (double)val;

        // Shift the origin to keep the chosen face fixed (see shiftFactor). Sound for horizontal axes
        // (centered dims) and for the floor's vertical thickness, where the axis is the only thing the
        // origin moves along.
        Transform3D xf = _origXform;
        xf.Origin += Axis * (_shiftFactor * (val - _orig));
        _inst.LocalTransform = xf;
    }

    public void Cancel()
    {
        _inst.Parameters[_param] = (double)_orig;
        _inst.LocalTransform = _origXform;
        _current = _orig;
    }

    public bool Changed => !Mathf.IsEqualApprox(_current, _orig);

    public ICommand Commit(Action refresh)
        => new ResizeInstanceCommand(_inst, _param, _orig, _current, _origXform, _inst.LocalTransform, refresh);

    private static float GetF(PrimitiveInstanceData d, string key)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : 0f;
}
