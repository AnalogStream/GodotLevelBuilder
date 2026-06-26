using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// Encodes a polygon floor's holes as two PARALLEL FLAT arrays — <c>Parameters["holes"]</c>
/// (Array&lt;Vector3&gt;, every hole's vertices concatenated) + <c>Parameters["holeSizes"]</c>
/// (Array&lt;float&gt;, the vertex count of each hole). Flat arrays of proven Variant-native types
/// round-trip safely (the same points+banks pattern), avoiding a nested Array&lt;Array&gt;. A single
/// legacy <c>Parameters["hole"]</c> (the first v2 single-hole format) is migrated on read.
/// </summary>
public static class PolygonHoles
{
    public const string VertsKey = "holes";
    public const string SizesKey = "holeSizes";
    public const string LegacyKey = "hole";

    /// <summary>Reads the holes as a list of rings (local space, each cleaned + ≥3 points). Prefers the
    /// flat holes/holeSizes pair; falls back to a single legacy "hole" ring.</summary>
    public static List<List<Vector3>> Decode(PrimitiveInstanceData inst)
    {
        var result = new List<List<Vector3>>();
        if (inst.Parameters.ContainsKey(VertsKey) && inst.Parameters.ContainsKey(SizesKey))
        {
            Array verts = inst.Parameters[VertsKey].AsGodotArray();
            Array sizes = inst.Parameters[SizesKey].AsGodotArray();
            int idx = 0;
            for (int h = 0; h < sizes.Count; h++)
            {
                int n = Mathf.RoundToInt(sizes[h].AsSingle());
                var ring = new List<Vector3>();
                for (int i = 0; i < n && idx < verts.Count; i++, idx++) ring.Add(verts[idx].AsVector3());
                AddIfValid(result, ring);
            }
        }
        else if (inst.Parameters.ContainsKey(LegacyKey))
        {
            var ring = new List<Vector3>();
            foreach (Variant v in inst.Parameters[LegacyKey].AsGodotArray()) ring.Add(v.AsVector3());
            AddIfValid(result, ring);
        }
        return result;
    }

    /// <summary>Packs rings (each ≥3 points) into the flat verts/sizes arrays.</summary>
    public static (Array<Vector3> verts, Array<float> sizes) Encode(IEnumerable<List<Vector3>> holes)
    {
        var verts = new Array<Vector3>();
        var sizes = new Array<float>();
        foreach (List<Vector3> ring in holes)
        {
            if (ring.Count < 3) continue;
            sizes.Add(ring.Count);
            foreach (Vector3 p in ring) verts.Add(p);
        }
        return (verts, sizes);
    }

    private static void AddIfValid(List<List<Vector3>> result, List<Vector3> ring)
    {
        // Drop near-coincident consecutive points + a closing duplicate, like the outline read.
        for (int i = ring.Count - 1; i > 0; i--)
            if (ring[i].DistanceTo(ring[i - 1]) <= 1e-3f) ring.RemoveAt(i);
        if (ring.Count >= 2 && ring[0].DistanceTo(ring[^1]) <= 1e-3f) ring.RemoveAt(ring.Count - 1);
        if (ring.Count >= 3) result.Add(ring);
    }
}
