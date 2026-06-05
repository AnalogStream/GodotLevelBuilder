using Godot;

namespace LevelBuilder.Core.Geometry;

/// <summary>Low-level mesh helpers shared by primitive generators.</summary>
public static class MeshBuilder
{
    /// <summary>
    /// Adds a quad (two triangles a-b-c, a-c-d) to the surface. Corners must be given
    /// in CCW order as seen from the <paramref name="normal"/> side so the front face
    /// points outward. UVs are planar in metres (1 unit = 1 metre).
    /// </summary>
    public static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        float u = a.DistanceTo(b);
        float v = a.DistanceTo(d);

        AddVertex(st, a, normal, new Vector2(0, 0));
        AddVertex(st, b, normal, new Vector2(u, 0));
        AddVertex(st, c, normal, new Vector2(u, v));

        AddVertex(st, a, normal, new Vector2(0, 0));
        AddVertex(st, c, normal, new Vector2(u, v));
        AddVertex(st, d, normal, new Vector2(0, v));
    }

    private static void AddVertex(SurfaceTool st, Vector3 p, Vector3 n, Vector2 uv)
    {
        st.SetNormal(n);
        st.SetUV(uv);
        st.AddVertex(p);
    }
}
