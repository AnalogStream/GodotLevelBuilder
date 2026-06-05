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
                AddLength(handles, inst, prim, world, l, h);
                AddCentered(handles, inst, prim, world, "thickness", new Vector3(0, 0, 1), t * 0.5f, new Vector3(0, h * 0.5f, 0));
                // Height grows upward from the fixed base (y=0) — no origin shift.
                AddFace(handles, inst, prim, world, "height", new Vector3(0, h, 0), new Vector3(0, 1, 0), 0f);
                break;
            }
            case "ramp":
            {
                float l = GetF(inst, "length", 3f), r = GetF(inst, "rise", 3f), w = GetF(inst, "width", 1.2f);
                var midH = new Vector3(0, r * 0.5f, 0);
                AddCentered(handles, inst, prim, world, "length", new Vector3(1, 0, 0), l * 0.5f, midH);
                AddCentered(handles, inst, prim, world, "width", new Vector3(0, 0, 1), w * 0.5f, midH);
                // Rise grows up from the fixed base (y=0), handled at the high (back) end.
                AddFace(handles, inst, prim, world, "rise", new Vector3(l * 0.5f, r, 0), new Vector3(0, 1, 0), 0f);
                break;
            }
            case "stairs":
            {
                float run = GetF(inst, "run", 3f), rise = GetF(inst, "totalRise", 3f), w = GetF(inst, "width", 1.2f);
                var midH = new Vector3(0, rise * 0.5f, 0);
                AddCentered(handles, inst, prim, world, "run", new Vector3(1, 0, 0), run * 0.5f, midH);
                AddCentered(handles, inst, prim, world, "width", new Vector3(0, 0, 1), w * 0.5f, midH);
                // Total rise grows up from the fixed base (y=0), handled at the high (back) end. (Step count not gizmo-editable.)
                AddFace(handles, inst, prim, world, "totalRise", new Vector3(run * 0.5f, rise, 0), new Vector3(0, 1, 0), 0f);
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

    /// <summary>
    /// Wall length: a centered handle on each end, but each also carries the wall's openings so they
    /// hold their world position. Dragging the +X end leaves the u=0 (−X) end — which offsets are
    /// measured from — fixed (openComp 0); dragging the −X end moves it the full growth (openComp 1).
    /// </summary>
    private static void AddLength(List<IEditHandle> handles, PrimitiveInstanceData inst, IPrimitive prim,
        Transform3D world, float l, float h)
    {
        (float min, float max) = Range(prim, "length");
        OpeningData[] openings = ToArray(inst.Openings);
        var perp = new Vector3(0, h * 0.5f, 0);
        AddResize(handles, inst, "length", min, max, world, new Vector3(l * 0.5f, 0, 0) + perp, new Vector3(1, 0, 0), 0.5f, openings, 0f);
        AddResize(handles, inst, "length", min, max, world, new Vector3(-l * 0.5f, 0, 0) + perp, new Vector3(-1, 0, 0), 0.5f, openings, 1f);
    }

    /// <summary>One handle: anchor + outward axis (local), with the origin-shift factor that fixes a face.</summary>
    private static void AddFace(List<IEditHandle> handles, PrimitiveInstanceData inst, IPrimitive prim,
        Transform3D world, string param, Vector3 localAnchor, Vector3 localAxis, float shiftFactor)
    {
        (float min, float max) = Range(prim, param);
        AddResize(handles, inst, param, min, max, world, localAnchor, localAxis, shiftFactor, null, 0f);
    }

    private static void AddResize(List<IEditHandle> handles, PrimitiveInstanceData inst, string param, float min, float max,
        Transform3D world, Vector3 localAnchor, Vector3 localAxis, float shiftFactor,
        OpeningData[] openings, float openComp)
    {
        Vector3 anchor = world * localAnchor;
        Vector3 axis = (world.Basis * localAxis).Normalized();
        handles.Add(new AxisResizeHandle(inst, param, min, max, anchor, axis, shiftFactor, openings, openComp));
    }

    private static OpeningData[] ToArray(Godot.Collections.Array<OpeningData> openings)
    {
        var arr = new OpeningData[openings.Count];
        for (int i = 0; i < openings.Count; i++) arr[i] = openings[i];
        return arr;
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
