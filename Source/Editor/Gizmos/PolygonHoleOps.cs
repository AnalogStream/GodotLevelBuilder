using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Shared operations for the hole-edit handles. A hole edit works from a COPY of the whole hole set
/// captured when the handle is built — never a fresh <see cref="PolygonHoles.Decode"/> mid-drag, because
/// Decode drops coincident / sub-3 rings and would desync the handle's hole/corner indices. The live
/// write and the undoable command's "to" are both derived from that held copy.
/// </summary>
public static class PolygonHoleOps
{
    /// <summary>A deep-enough copy (Vector3 is a value type) so edits don't mutate the captured snapshot.</summary>
    public static List<List<Vector3>> Clone(List<List<Vector3>> holes)
    {
        var r = new List<List<Vector3>>(holes.Count);
        foreach (List<Vector3> ring in holes) r.Add(new List<Vector3>(ring));
        return r;
    }

    /// <summary>Writes the hole set onto the instance's flat arrays for the live preview (no command).</summary>
    public static void WriteLive(PrimitiveInstanceData inst, List<List<Vector3>> holes)
    {
        (Array<Vector3> v, Array<float> s) = PolygonHoles.Encode(holes);
        inst.Parameters[PolygonHoles.VertsKey] = v;
        inst.Parameters[PolygonHoles.SizesKey] = s;
    }

    /// <summary>One undoable hole-set replacement, encoded from the held copies.</summary>
    public static ICommand Command(PrimitiveInstanceData inst,
        List<List<Vector3>> from, List<List<Vector3>> to, Action refresh)
    {
        (Array<Vector3> fv, Array<float> fs) = PolygonHoles.Encode(from);
        (Array<Vector3> tv, Array<float> ts) = PolygonHoles.Encode(to);
        return new SetHolesCommand(inst, fv, fs, tv, ts, refresh);
    }

    public static Vector3 Centroid(List<Vector3> ring)
    {
        Vector3 c = Vector3.Zero;
        foreach (Vector3 p in ring) c += p;
        return ring.Count > 0 ? c / ring.Count : c;
    }
}
