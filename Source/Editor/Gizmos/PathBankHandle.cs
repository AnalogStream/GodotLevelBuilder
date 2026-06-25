using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Rolls one control point's bank (the per-point cross-section tilt for banked turns / loops). An
/// orange widget on an arm out the point's lateral side; drag it up/down and the vertical travel maps
/// to a roll angle (raising the arm tip by dy on an arm of length L ≈ asin(dy/L)), added to the point's
/// current bank and snapped to 5°. Points stay put — only the parallel "banks" array changes — so it
/// commits an <see cref="EditPathCommand"/> with the points unchanged and the banks updated.
/// </summary>
public sealed class PathBankHandle : IEditHandle, IStyledHandle
{
    private const float Arm = 1.0f;
    private const float Lift = 0.2f;   // raise the widget a touch so it isn't buried in the surface
    private const float SnapDeg = 5f;
    private const float MaxDeg = 89f;

    private readonly PrimitiveInstanceData _inst;
    private readonly int _index;
    private readonly Vector3 _anchor;  // world arm tip
    private readonly Array<Vector3> _origPoints;
    private readonly Array<float> _origBanks;
    private readonly float _origDeg;
    private float _deg;

    public PathBankHandle(PrimitiveInstanceData inst, int index, Vector3 worldPoint, Vector3 lateral)
    {
        _inst = inst;
        _index = index;
        _origPoints = PathPoints.Read(inst);
        _origBanks = PathPoints.ReadBanks(inst, _origPoints.Count);
        _origDeg = _origBanks[index];
        _deg = _origDeg;
        _anchor = worldPoint + lateral * Arm + Vector3.Up * Lift;
    }

    public Color WidgetColor => new(1.0f, 0.6f, 0.1f);
    public float WidgetScale => 0.85f;

    public Vector3 Anchor => _anchor;

    public bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world)
        => GizmoMath.ClosestOnAxis(_anchor, Vector3.Up, rayFrom, rayDir.Normalized(), out world);

    public void Preview(Vector3 grabStart, Vector3 grabNow)
    {
        // Drag the arm UP to lift the edge it sits on: a +lateral arm tilts toward −up under a positive
        // bank (R' = R·cos a − U·sin a in the right-handed frame), so map upward drag to a negative bank
        // for the grabbed edge to follow the cursor.
        float dy = grabNow.Y - grabStart.Y;
        float add = -Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(dy / Arm, -1f, 1f)));
        _deg = Mathf.Clamp(Mathf.Round((_origDeg + add) / SnapDeg) * SnapDeg, -MaxDeg, MaxDeg);

        Array<float> banks = _origBanks.Duplicate();
        banks[_index] = _deg;
        _inst.Parameters["banks"] = banks;
    }

    public void Cancel()
    {
        _inst.Parameters["banks"] = _origBanks.Duplicate();
        _deg = _origDeg;
    }

    public bool Changed => !Mathf.IsEqualApprox(_deg, _origDeg);

    public ICommand Commit(Action refresh)
        => new EditPathCommand(_inst, _origPoints, _origBanks,
            _origPoints.Duplicate(), PathPoints.ReadBanks(_inst, _origPoints.Count), refresh);
}
