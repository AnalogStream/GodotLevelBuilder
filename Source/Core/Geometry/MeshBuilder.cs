using Godot;

namespace LevelBuilder.Core.Geometry;

/// <summary>Low-level mesh helpers shared by primitive generators.</summary>
public static class MeshBuilder
{
    /// <summary>
    /// Adds a quad with corners a,b,c,d given in CCW order as seen from the
    /// <paramref name="normal"/> side. Emitted triangles use Godot's clockwise
    /// front-face winding (a-c-b, a-d-c) so the outward side renders and the inside
    /// is culled. UVs are planar in metres (1 unit = 1 metre).
    /// </summary>
    public static void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
    {
        // UVs are planar in metres (1 unit = 1 metre). V is flipped (a,b at V=v; c,d at V=0)
        // because Godot's UV origin is the texture's top-left with V increasing DOWN, while the
        // a->d edge runs "up" the face — without the flip every textured face renders upside-down
        // (and reads as a left-right mirror on a floor viewed top-down).
        float u = a.DistanceTo(b);
        float v = a.DistanceTo(d);
        var uvA = new Vector2(0, v);
        var uvB = new Vector2(u, v);
        var uvC = new Vector2(u, 0);
        var uvD = new Vector2(0, 0);

        AddVertex(st, a, normal, uvA);
        AddVertex(st, c, normal, uvC);
        AddVertex(st, b, normal, uvB);

        AddVertex(st, a, normal, uvA);
        AddVertex(st, d, normal, uvD);
        AddVertex(st, c, normal, uvC);
    }

    private static void AddVertex(SurfaceTool st, Vector3 p, Vector3 n, Vector2 uv)
    {
        st.SetNormal(n);
        st.SetUV(uv);
        st.AddVertex(p);
    }

    /// <summary>A single-surface box of the given size, centred on the origin.</summary>
    public static ArrayMesh Box(Vector3 size)
    {
        float hx = size.X * 0.5f, hy = size.Y * 0.5f, hz = size.Z * 0.5f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        AddQuad(st, new(-hx, -hy, hz), new(hx, -hy, hz), new(hx, hy, hz), new(-hx, hy, hz), new Vector3(0, 0, 1));   // +Z
        AddQuad(st, new(hx, -hy, -hz), new(-hx, -hy, -hz), new(-hx, hy, -hz), new(hx, hy, -hz), new Vector3(0, 0, -1)); // -Z
        AddQuad(st, new(-hx, hy, hz), new(hx, hy, hz), new(hx, hy, -hz), new(-hx, hy, -hz), Vector3.Up);              // +Y
        AddQuad(st, new(-hx, -hy, -hz), new(hx, -hy, -hz), new(hx, -hy, hz), new(-hx, -hy, hz), Vector3.Down);        // -Y
        AddQuad(st, new(hx, -hy, hz), new(hx, -hy, -hz), new(hx, hy, -hz), new(hx, hy, hz), new Vector3(1, 0, 0));    // +X
        AddQuad(st, new(-hx, -hy, -hz), new(-hx, -hy, hz), new(-hx, hy, hz), new(-hx, hy, -hz), new Vector3(-1, 0, 0)); // -X

        var mesh = new ArrayMesh();
        st.GenerateTangents();
        st.Commit(mesh);
        return mesh;
    }
}
