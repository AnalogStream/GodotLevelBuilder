using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>Shared read of a path_sweep instance's control points for the point-edit handles.</summary>
internal static class PathPoints
{
    /// <summary>A fresh, independent <c>Array&lt;Vector3&gt;</c> of the instance's control points (local space).
    /// Read element-wise (no typed-array marshal) to dodge the silent-empty trap, and without the
    /// primitive's coincident-point dedup so handle indices line up with the stored points.</summary>
    public static Array<Vector3> Read(PrimitiveInstanceData inst)
    {
        var result = new Array<Vector3>();
        if (inst.Parameters.ContainsKey("points"))
            foreach (Variant v in inst.Parameters["points"].AsGodotArray())
                result.Add(v.AsVector3());
        return result;
    }

    /// <summary>The per-point bank list (degrees), padded/truncated to exactly <paramref name="count"/> so it
    /// stays length-aligned with the points. Missing entries (old saves / freshly drawn paths) read as 0.</summary>
    public static Array<float> ReadBanks(PrimitiveInstanceData inst, int count)
    {
        Array raw = inst.Parameters.ContainsKey("banks") ? inst.Parameters["banks"].AsGodotArray() : new Array();
        var result = new Array<float>();
        for (int i = 0; i < count; i++) result.Add(i < raw.Count ? raw[i].AsSingle() : 0f);
        return result;
    }
}
