using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Builds the resize handles for a selected instance. Knows each primitive's editable dimensions
/// and where their face handles sit (local space); the generic <see cref="AxisResizeHandle"/> does
/// the dragging. Add a case here when a new primitive type gains resizable dimensions.
/// </summary>
public static class InstanceHandleProvider
{
    public static List<IEditHandle> Build(PrimitiveInstanceData inst, IPrimitive prim, Vector3 elevationOffset)
    {
        var handles = new List<IEditHandle>();
        if (prim == null) return handles;

        // World transform of the instance (basis + storey-local origin + storey elevation).
        var world = new Transform3D(inst.LocalTransform.Basis, inst.LocalTransform.Origin + elevationOffset);

        switch (prim.TypeId)
        {
            case "floor":
            {
                float w = GetF(inst, "width", 4f), d = GetF(inst, "depth", 4f), t = GetF(inst, "thickness", 0.2f);
                AddCentered(handles, inst, prim, world, "width", new Vector3(1, 0, 0), w * 0.5f, Vector3.Zero);
                AddCentered(handles, inst, prim, world, "depth", new Vector3(0, 0, 1), d * 0.5f, Vector3.Zero);
                // Thickness from both faces: bottom handle grows down (top fixed), top handle grows up (bottom fixed).
                AddFace(handles, inst, prim, world, "thickness", new Vector3(0, -t, 0), new Vector3(0, -1, 0), 0f);
                AddFace(handles, inst, prim, world, "thickness", new Vector3(0, 0, 0), new Vector3(0, 1, 0), 1f);
                break;
            }
            case "wall":
            {
                float l = GetF(inst, "length", 1f), h = GetF(inst, "height", 3f), t = GetF(inst, "thickness", 0.2f);
                AddCentered(handles, inst, prim, world, "length", new Vector3(1, 0, 0), l * 0.5f, new Vector3(0, h * 0.5f, 0));
                AddCentered(handles, inst, prim, world, "thickness", new Vector3(0, 0, 1), t * 0.5f, new Vector3(0, h * 0.5f, 0));
                // Height grows upward from the fixed base (y=0) — no origin shift.
                AddFace(handles, inst, prim, world, "height", new Vector3(0, h, 0), new Vector3(0, 1, 0), 0f);
                break;
            }
        }
        return handles;
    }

    /// <summary>
    /// A centered dimension gets a handle on each face. The handle sits at
    /// <c>±axis·halfExtent + perpOffset</c>: only the on-axis half flips between the two faces, the
    /// perpendicular placement (e.g. a wall handle's mid-height) is the same on both. Dragging either
    /// keeps the far face fixed (shiftFactor 0.5).
    /// </summary>
    private static void AddCentered(List<IEditHandle> handles, PrimitiveInstanceData inst, IPrimitive prim,
        Transform3D world, string param, Vector3 localAxis, float halfExtent, Vector3 perpOffset)
    {
        AddFace(handles, inst, prim, world, param, localAxis * halfExtent + perpOffset, localAxis, 0.5f);
        AddFace(handles, inst, prim, world, param, -localAxis * halfExtent + perpOffset, -localAxis, 0.5f);
    }

    /// <summary>One handle: anchor + outward axis (local), with the origin-shift factor that fixes a face.</summary>
    private static void AddFace(List<IEditHandle> handles, PrimitiveInstanceData inst, IPrimitive prim,
        Transform3D world, string param, Vector3 localAnchor, Vector3 localAxis, float shiftFactor)
    {
        (float min, float max) = Range(prim, param);
        Vector3 anchor = world * localAnchor;
        Vector3 axis = (world.Basis * localAxis).Normalized();
        handles.Add(new AxisResizeHandle(inst, param, min, max, anchor, axis, shiftFactor));
    }

    private static (float, float) Range(IPrimitive prim, string key)
    {
        foreach (ParamSpec spec in prim.Parameters)
            if (spec.Key == key) return (spec.Min, spec.Max);
        return (0.01f, 1000f);
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
