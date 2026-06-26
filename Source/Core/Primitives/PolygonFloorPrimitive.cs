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
/// An optional single hole ring lives in <c>Parameters["hole"]</c> (same local space; ≥3 points). Rather
/// than triangulate a polygon-with-hole directly (CLAUDE.md gotcha #1), the hole is BRIDGED into the
/// outline — a zero-width seam stitches the two rings into one simple polygon that
/// <see cref="Geometry2D.TriangulatePolygon"/> handles — and the hole gets inward-facing side walls, so
/// the trimesh collision has a real void (the ball falls through). The hole is ALL-OR-NOTHING: if the
/// bridge/triangulation fails (e.g. a ring drawn partly outside the outline, the common state mid-draw),
/// the slab renders SOLID (no hole, no hole walls) rather than producing garbage — so a hole drawn
/// outside the outline silently reads as no hole. Multiple holes are not supported (one ring only).
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
        List<Vector3> pts = ReadPoints(data, "points");
        if (pts.Count < 3) return mesh; // need a triangle's worth of outline

        float t = GetF(data, "thickness", 0.2f);

        // 2D outline (XZ) for triangulation; centroid for the fan fallback + outward side-wall normals.
        var outer2d = new Vector2[pts.Count];
        Vector3 centre = Vector3.Zero;
        for (int i = 0; i < pts.Count; i++)
        {
            outer2d[i] = new Vector2(pts[i].X, pts[i].Z);
            centre += pts[i];
        }
        centre /= pts.Count;
        var centroid2d = new Vector2(centre.X, centre.Z);

        // Optional hole: bridge it into the outline so a single simple polygon triangulates the holed cap.
        // All-or-nothing — only if the bridge AND its triangulation succeed do we use the holed cap and emit
        // the hole walls; otherwise the slab stays solid (a ring drawn outside the outline reads as no hole).
        List<Vector3> hole = ReadPoints(data, "hole");
        Vector2[] capVerts = outer2d;
        int[] capTris = null;
        bool holed = false;
        if (hole.Count >= 3)
        {
            var hole2d = new Vector2[hole.Count];
            for (int i = 0; i < hole.Count; i++) hole2d[i] = new Vector2(hole[i].X, hole[i].Z);
            List<Vector2> combined = BridgeHole(outer2d, hole2d);
            int[] ctris = combined != null ? Geometry2D.TriangulatePolygon(combined.ToArray()) : null;
            if (ctris != null && ctris.Length >= 3) { capVerts = combined.ToArray(); capTris = ctris; holed = true; }
        }
        if (!holed) capTris = Geometry2D.TriangulatePolygon(outer2d); // solid; null while non-simple → fan fallback

        // Surface 0: Top cap (y = 0, faces +Y).  Surface 1: Bottom cap (y = -t, faces -Y).
        SurfaceTool top = Begin();
        EmitCap(top, capVerts, capTris, 0, Vector3.Up, centroid2d);
        Commit(top, mesh);

        SurfaceTool bottom = Begin();
        EmitCap(bottom, capVerts, capTris, -t, Vector3.Down, centroid2d);
        Commit(bottom, mesh);

        // Surface 2: Edge walls — one vertical quad per outline edge (faced outward, away from the centroid),
        // plus the hole's walls (faced inward, toward the hole centroid) when the hole is live.
        SurfaceTool edge = Begin();
        AddWalls(edge, pts, t, centre, outward: true);
        if (holed) AddWalls(edge, hole, t, Centroid(hole), outward: false);
        Commit(edge, mesh);

        return mesh;
    }

    /// <summary>Emits a vertical quad per ring edge. <paramref name="outward"/> faces them away from
    /// <paramref name="reference"/> (the outline, seen from outside); else toward it (a hole, seen from
    /// inside the void).</summary>
    private static void AddWalls(SurfaceTool edge, List<Vector3> ring, float t, Vector3 reference, bool outward)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % ring.Count];
            var topA = new Vector3(a.X, 0, a.Z);
            var topB = new Vector3(b.X, 0, b.Z);
            var botB = new Vector3(b.X, -t, b.Z);
            var botA = new Vector3(a.X, -t, a.Z);
            Vector3 mid = (topA + topB) * 0.5f;
            Vector3 face = outward ? mid - reference : reference - mid; face.Y = 0;
            if (face.LengthSquared() < 1e-9f) face = Vector3.Right;
            MeshBuilder.AddQuadFacing(edge, topA, topB, botB, botA, face);
        }
    }

    private static Vector3 Centroid(List<Vector3> ring)
    {
        Vector3 c = Vector3.Zero;
        foreach (Vector3 p in ring) c += p;
        return c / ring.Count;
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

    /// <summary>Reads a ring (the outline "points" or the "hole") from a parameter, dropping near-coincident
    /// consecutive points and a closing duplicate (last == first) so the implicit ring isn't double-counted.
    /// Untyped element read (AsGodotArray + AsVector3) dodges the typed-array silent-empty trap (DATA_MODEL.md).</summary>
    private static List<Vector3> ReadPoints(PrimitiveInstanceData data, string key)
    {
        var pts = new List<Vector3>();
        if (!data.Parameters.ContainsKey(key)) return pts;

        Godot.Collections.Array raw = data.Parameters[key].AsGodotArray();
        for (int i = 0; i < raw.Count; i++)
        {
            Vector3 p = raw[i].AsVector3();
            if (pts.Count > 0 && p.DistanceTo(pts[^1]) <= 1e-3f) continue;
            pts.Add(p);
        }
        if (pts.Count >= 2 && pts[0].DistanceTo(pts[^1]) <= 1e-3f) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    // --- Hole bridging. Stitches the hole ring into the outline through a zero-width seam so the result is
    // one simple polygon TriangulatePolygon can handle (CLAUDE.md gotcha #1: don't triangulate a hole
    // directly). The construction (force outer CCW / hole CW, ray the hole's max-X vertex to the nearest
    // outline edge, refine to a visible vertex, stitch) was verified to yield a simple polygon across many
    // cases (off-centre / rotated holes, non-convex and many-sided outlines) in a standalone test harness.

    /// <summary>Outer + hole → one simple polygon (or null if the hole's +X ray misses the outline, i.e. the
    /// hole isn't inside). Coordinates are (X, Z) packed into a <see cref="Vector2"/>.</summary>
    private static List<Vector2> BridgeHole(Vector2[] outerIn, Vector2[] holeIn)
    {
        Vector2[] outer = EnsureWinding(outerIn, ccw: true);
        Vector2[] hole = EnsureWinding(holeIn, ccw: false);
        int no = outer.Length, nh = hole.Length;

        int mi = 0;
        for (int i = 1; i < nh; i++) if (hole[i].X > hole[mi].X) mi = i;
        Vector2 m = hole[mi];

        float bestX = float.MaxValue; int e = -1; Vector2 inter = default;
        for (int i = 0; i < no; i++)
        {
            Vector2 a = outer[i], b = outer[(i + 1) % no];
            if ((a.Y > m.Y) == (b.Y > m.Y)) continue;            // edge doesn't straddle the horizontal ray
            float tt = (m.Y - a.Y) / (b.Y - a.Y);
            float x = a.X + tt * (b.X - a.X);
            if (x >= m.X - 1e-6f && x < bestX) { bestX = x; e = i; inter = new Vector2(x, m.Y); }
        }
        if (e < 0) return null;

        int pi = outer[e].X > outer[(e + 1) % no].X ? e : (e + 1) % no;
        Vector2 p = outer[pi];

        // Refine to the visible outline vertex: among vertices inside triangle (m, inter, p), pick the one at
        // the smallest angle to the +X ray (max cos), ties broken by nearest — otherwise m→p would be blocked.
        float bestCos = -2f, bestDist = float.MaxValue;
        for (int i = 0; i < no; i++)
        {
            if (i == pi) continue;
            Vector2 r = outer[i];
            if (!PointInTri(r, m, inter, p)) continue;
            Vector2 d = r - m; float len = d.Length();
            if (len < 1e-9f) continue;
            float cos = d.X / len;
            if (cos > bestCos + 1e-7f || (Mathf.Abs(cos - bestCos) <= 1e-7f && len < bestDist))
            { bestCos = cos; bestDist = len; pi = i; p = r; } // shrink the triangle as the validated harness does
        }

        var res = new List<Vector2>();
        for (int k = 0; k <= pi; k++) res.Add(outer[k]);
        for (int k = 0; k <= nh; k++) res.Add(hole[(mi + k) % nh]); // m .. around .. m
        res.Add(outer[pi]);                                          // bridge back to p
        for (int k = pi + 1; k < no; k++) res.Add(outer[k]);
        return res;
    }

    private static Vector2[] EnsureWinding(Vector2[] p, bool ccw)
    {
        float area = 0;
        for (int i = 0; i < p.Length; i++) { Vector2 u = p[i], v = p[(i + 1) % p.Length]; area += u.X * v.Y - v.X * u.Y; }
        if ((area > 0) == ccw) return p;
        var r = new Vector2[p.Length];
        for (int i = 0; i < p.Length; i++) r[i] = p[p.Length - 1 - i];
        return r;
    }

    private static float Orient(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool PointInTri(Vector2 q, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Orient(a, b, q), d2 = Orient(b, c, q), d3 = Orient(c, a, q);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
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
