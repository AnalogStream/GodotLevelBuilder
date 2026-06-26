using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A flat floor slab whose outline is an arbitrary polygon (not the axis-aligned width×depth box of
/// <see cref="FloorPrimitive"/>) — for angled / rhombus / freeform playfields. The outline is a list of
/// control points stored in <c>Parameters["points"]</c> (local space, relative to the instance origin,
/// Y ignored — the slab is planar), treated as a CLOSED ring. The top face sits at local y = 0 and the
/// slab extends down by <c>thickness</c>.
///
/// The top/bottom caps are triangulated with <see cref="Geometry2D.TriangulatePolygon"/> (which the path
/// tool already uses for its end caps); each triangle is oriented by its own geometric normal toward
/// ±Y, so the polygon's winding doesn't matter. When that returns nothing — the outline is momentarily
/// SELF-INTERSECTING, which happens constantly while rubber-banding the live preview — the cap falls back
/// to a centroid fan (exact for a convex polygon, and the preview never blanks out). The sides are one
/// vertical quad per edge, oriented outward from the polygon centroid. Surfaces: 0 = Top, 1 = Bottom, 2 = Edge.
///
/// v1 is a SOLID polygon (no holes). A hole would be a polygon-with-hole, which CLAUDE.md gotcha #1 says
/// not to triangulate directly — that's a later slice (bridge the hole ring into the outline, or
/// decompose into hole-free quads).
/// </summary>
public sealed class PolygonFloorPrimitive : IPrimitive
{
    public string TypeId => "polygon_floor";
    public string DisplayName => "Polygon Floor";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 100f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Top", "Bottom", "Edge" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        var mesh = new ArrayMesh();
        List<Vector3> pts = ReadPoints(data);
        if (pts.Count < 3) return mesh; // need a triangle's worth of outline

        float t = GetF(data, "thickness", 0.2f);

        // 2D outline (XZ) for triangulation; centroid for the fan fallback + outward side-wall normals.
        var poly2d = new Vector2[pts.Count];
        Vector3 centre = Vector3.Zero;
        for (int i = 0; i < pts.Count; i++)
        {
            poly2d[i] = new Vector2(pts[i].X, pts[i].Z);
            centre += pts[i];
        }
        centre /= pts.Count;
        var centroid2d = new Vector2(centre.X, centre.Z);

        int[] tris = Geometry2D.TriangulatePolygon(poly2d); // empty/null while the outline is non-simple

        // Surface 0: Top cap (y = 0, faces +Y).  Surface 1: Bottom cap (y = -t, faces -Y).
        SurfaceTool top = Begin();
        EmitCap(top, poly2d, tris, 0, Vector3.Up, centroid2d);
        Commit(top, mesh);

        SurfaceTool bottom = Begin();
        EmitCap(bottom, poly2d, tris, -t, Vector3.Down, centroid2d);
        Commit(bottom, mesh);

        // Surface 2: Edge walls — one vertical quad per outline edge, faced away from the centroid.
        SurfaceTool edge = Begin();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
            var topA = new Vector3(a.X, 0, a.Z);
            var topB = new Vector3(b.X, 0, b.Z);
            var botB = new Vector3(b.X, -t, b.Z);
            var botA = new Vector3(a.X, -t, a.Z);
            Vector3 mid = (topA + topB) * 0.5f;
            Vector3 outward = mid - centre; outward.Y = 0;
            if (outward.LengthSquared() < 1e-9f) outward = Vector3.Right;
            MeshBuilder.AddQuadFacing(edge, topA, topB, botB, botA, outward);
        }
        Commit(edge, mesh);

        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
    {
        ArrayMesh mesh = BuildMesh(data, ctx);
        if (mesh.GetSurfaceCount() == 0) return System.Array.Empty<Shape3D>();
        return new Shape3D[] { mesh.CreateTrimeshShape() };
    }

    /// <summary>
    /// Emits one flat cap at height <paramref name="y"/> facing <paramref name="face"/>. Uses the
    /// <see cref="Geometry2D.TriangulatePolygon"/> result when it's valid (any simple polygon); otherwise
    /// falls back to a fan from the centroid — exact for a convex polygon and, critically, keeps the live
    /// preview visible while the in-progress outline is momentarily self-intersecting (triangulation
    /// returns nothing then). Each triangle is oriented by its own normal toward <paramref name="face"/>.
    /// </summary>
    private static void EmitCap(SurfaceTool st, Vector2[] poly2d, int[] tris, float y, Vector3 face, Vector2 centroid)
    {
        if (tris != null && tris.Length >= 3)
        {
            for (int k = 0; k + 2 < tris.Length; k += 3)
                MeshBuilder.AddTriFacing(st,
                    new Vector3(poly2d[tris[k]].X, y, poly2d[tris[k]].Y),
                    new Vector3(poly2d[tris[k + 1]].X, y, poly2d[tris[k + 1]].Y),
                    new Vector3(poly2d[tris[k + 2]].X, y, poly2d[tris[k + 2]].Y),
                    face);
            return;
        }
        var c = new Vector3(centroid.X, y, centroid.Y);
        int n = poly2d.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly2d[i], b = poly2d[(i + 1) % n];
            MeshBuilder.AddTriFacing(st, c, new Vector3(a.X, y, a.Y), new Vector3(b.X, y, b.Y), face);
        }
    }

    /// <summary>Reads the outline points, dropping near-coincident consecutive points and a closing
    /// duplicate (last == first) so the implicit ring isn't double-counted. Untyped element read
    /// (AsGodotArray + AsVector3) dodges the typed-array silent-empty trap (see DATA_MODEL.md).</summary>
    private static List<Vector3> ReadPoints(PrimitiveInstanceData data)
    {
        var pts = new List<Vector3>();
        if (!data.Parameters.ContainsKey("points")) return pts;

        Godot.Collections.Array raw = data.Parameters["points"].AsGodotArray();
        for (int i = 0; i < raw.Count; i++)
        {
            Vector3 p = raw[i].AsVector3();
            if (pts.Count > 0 && p.DistanceTo(pts[^1]) <= 1e-3f) continue;
            pts.Add(p);
        }
        if (pts.Count >= 2 && pts[0].DistanceTo(pts[^1]) <= 1e-3f) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    private static SurfaceTool Begin()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        return st;
    }

    private static void Commit(SurfaceTool st, ArrayMesh mesh)
    {
        st.GenerateTangents();
        st.Commit(mesh);
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
