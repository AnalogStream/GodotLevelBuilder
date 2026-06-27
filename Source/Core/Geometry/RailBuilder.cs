using Godot;

namespace LevelBuilder.Core.Geometry;

/// <summary>
/// Auto-rail generator shared by the polygon-outline primitives (polygon floor, circle plane, half-circle).
/// A rim swept along a ring or polyline (in XZ), sitting ON TOP of a slab whose top is local y=0 (so the
/// rail rises 0→h). Four styles, value-stable for serialization: 0 None, 1 Rail (solid curb/lip), 2 Elevated
/// Rail (posts + top beam / fence), 3 Bank (an angled wedge that funnels the ball back toward centre).
///
/// The ring is offset by an inset RUN into a mitered inset ring; each style emits a per-edge cross-section
/// between the ring and the inset. Winding is delegated to <see cref="MeshBuilder.AddQuadFacing"/> /
/// <see cref="MeshBuilder.AddTriFacing"/> (each face just states which way it should look), so there's no
/// hand-tracked CCW.
///
/// <see cref="EmitRing"/> sweeps a CLOSED ring (polygon outline, holes, full circle) — wraps, no end caps.
/// <see cref="EmitOpen"/> sweeps an OPEN polyline (a half-circle's arc or its diameter) — doesn't wrap, and
/// CAPS both open ends so a curb/bank run isn't hollow at its ends. This was extracted verbatim from
/// PolygonFloorPrimitive (the closed path is byte-for-byte the same behaviour); the open path is the addition.
/// </summary>
public static class RailBuilder
{
    /// <summary>Builds one rim along the CLOSED ring <paramref name="poly"/> at height <paramref name="h"/>.
    /// <paramref name="w"/> is the rail width (curb thickness / post + beam cross-section); the Bank style
    /// instead derives its inset RUN from the angle (run = h/tan(angle)) so the slope sits at exactly that
    /// angle. <paramref name="inward"/> insets toward the centroid (outer outline); false insets outward into
    /// the solid (a hole rim). The run is clamped so a wide rim can't invert a small ring. Returns false
    /// (nothing emitted) when style is None/unknown.</summary>
    public static bool EmitRing(SurfaceTool rail, Vector2[] poly, Vector2 centroid,
                                int style, float h, float w, float bankAngleDeg, bool inward, float maxInset = -1f)
    {
        if (style == 0 || poly.Length < 3) return false;
        float limit = InsetLimit(poly, closed: true, maxInset);
        w = Mathf.Min(w, limit);
        bool bankFlip = bankAngleDeg < 0f;
        float run = RunFor(style, h, w, bankAngleDeg, limit);
        Vector2[] inset = InsetRingClosed(poly, centroid, inward ? run : -run);
        switch (style)
        {
            case 1: BuildCurb(rail, poly, inset, h, closed: true); return true;
            case 2: BuildFence(rail, poly, h, w, closed: true); return true;
            case 3: BuildBank(rail, poly, inset, h, bankFlip, closed: true); return true;
            default: return false;
        }
    }

    /// <summary>Builds one rim along the OPEN polyline <paramref name="verts"/> (vertices in order, NOT
    /// wrapped) at height <paramref name="h"/>, insetting toward <paramref name="centroid"/>. Same styles as
    /// <see cref="EmitRing"/>, but the run is a finite strip: a curb/bank gets an END CAP at each open end so
    /// it isn't hollow, and a fence's end posts already cap it. Returns false for None / too-short / unknown.</summary>
    public static bool EmitOpen(SurfaceTool rail, Vector2[] verts, Vector2 centroid,
                                int style, float h, float w, float bankAngleDeg, float maxInset = -1f)
    {
        if (style == 0 || verts.Length < 2) return false;
        float limit = InsetLimit(verts, closed: false, maxInset);
        w = Mathf.Min(w, limit);
        bool bankFlip = bankAngleDeg < 0f;
        float run = RunFor(style, h, w, bankAngleDeg, limit);
        Vector2[] inset = InsetOpen(verts, centroid, run);
        switch (style)
        {
            case 1: BuildCurb(rail, verts, inset, h, closed: false); return true;
            case 2: BuildFence(rail, verts, h, w, closed: false); return true;
            case 3: BuildBank(rail, verts, inset, h, bankFlip, closed: false); return true;
            default: return false;
        }
    }

    /// <summary>How far the rim may inset before it risks inverting the ring. By default 0.45× the shortest
    /// edge (the polygon-floor heuristic, kept byte-identical for that path). A caller with a better bound —
    /// a finely-tessellated convex disc, where the tiny facet length would over-clamp the rim to a sliver —
    /// passes <paramref name="maxInset"/> (e.g. a fraction of the radius) to use that instead.</summary>
    private static float InsetLimit(Vector2[] poly, bool closed, float maxInset)
        => maxInset > 0f ? maxInset : 0.45f * MinEdge(poly, closed);

    /// <summary>The inset run for a style: Bank derives it from |angle| (run = h/tan|angle|), the rest use the
    /// rail width. Clamped to <paramref name="limit"/> so a wide rim can't invert the ring.</summary>
    private static float RunFor(int style, float h, float w, float bankAngleDeg, float limit)
    {
        float mag = Mathf.Clamp(Mathf.Abs(bankAngleDeg), 5f, 85f);  // clamp away from 0 so tan can't blow up
        float run = style == 3 ? h / Mathf.Tan(Mathf.DegToRad(mag)) : w;
        return Mathf.Min(run, limit);
    }

    /// <summary>Solid curb / lip: an outer vertical wall (at the outline), an inner vertical wall (at the
    /// inset), and a flat top band joining them. No underside — it sits flush on the slab top. For an open run
    /// the two ends are capped by a vertical cross-section quad each.</summary>
    private static void BuildCurb(SurfaceTool rail, Vector2[] outer, Vector2[] inset, float h, bool closed)
    {
        int n = outer.Length;
        int edges = closed ? n : n - 1;
        for (int i = 0; i < edges; i++)
        {
            int m = (i + 1) % n;
            Vector2 a = outer[i], b = outer[m], ai = inset[i], bi = inset[m];
            Vector3 Lo(Vector2 p) => new(p.X, 0, p.Y);
            Vector3 Hi(Vector2 p) => new(p.X, h, p.Y);

            // Outward horizontal: outline edge midpoint minus inset edge midpoint (the inset is inward).
            Vector3 outward = Lo(a) + Lo(b) - Lo(ai) - Lo(bi); outward.Y = 0;
            if (outward.LengthSquared() < 1e-9f) outward = Vector3.Right;

            MeshBuilder.AddQuadFacing(rail, Lo(a),  Lo(b),  Hi(b),  Hi(a),  outward);   // outer wall (faces out)
            MeshBuilder.AddQuadFacing(rail, Lo(ai), Lo(bi), Hi(bi), Hi(ai), -outward);  // inner wall (faces in)
            MeshBuilder.AddQuadFacing(rail, Hi(a),  Hi(b),  Hi(bi), Hi(ai), Vector3.Up);// top band (faces up)
        }

        if (!closed) CapEnds(outer, inset, h, CurbCap);

        void CurbCap(Vector2 o, Vector2 ins, Vector3 facing)
        {
            Vector3 Lo(Vector2 p) => new(p.X, 0, p.Y);
            Vector3 Hi(Vector2 p) => new(p.X, h, p.Y);
            MeshBuilder.AddQuadFacing(rail, Lo(o), Lo(ins), Hi(ins), Hi(o), facing);
        }
    }

    /// <summary>Angled bank: a wedge per edge — a vertical lip plus a slope between the outline and the inset.
    /// The slope's angle is set by the caller via the inset distance (run = h/tan(angle)). By default the lip
    /// is at the OUTLINE and the slope falls inward to the slab at the inset (funnels toward centre). When
    /// <paramref name="flip"/> is set the wedge LEANS THE OTHER WAY — the lip moves to the INSET and the slope
    /// falls back out to the slab at the outline. Underside sits flush on the slab; adjacent wedges share their
    /// lip-top and floor vertices (mitered inset), so the join is watertight without internal end caps. An open
    /// run caps its two ends with the wedge's triangular profile.</summary>
    private static void BuildBank(SurfaceTool rail, Vector2[] outer, Vector2[] inset, float h, bool flip, bool closed)
    {
        int n = outer.Length;
        int edges = closed ? n : n - 1;
        for (int i = 0; i < edges; i++)
        {
            int m = (i + 1) % n;
            Vector2 a = outer[i], b = outer[m], ai = inset[i], bi = inset[m];
            Vector3 Lo(Vector2 p) => new(p.X, 0, p.Y);
            Vector3 Hi(Vector2 p) => new(p.X, h, p.Y);

            Vector3 outward = Lo(a) + Lo(b) - Lo(ai) - Lo(bi); outward.Y = 0;
            if (outward.LengthSquared() < 1e-9f) outward = Vector3.Right;

            if (!flip)
            {
                MeshBuilder.AddQuadFacing(rail, Lo(a), Lo(b), Hi(b), Hi(a), outward);       // vertical outer lip
                MeshBuilder.AddQuadFacing(rail, Hi(a), Hi(b), Lo(bi), Lo(ai), Vector3.Up);  // slope falls inward
            }
            else
            {
                MeshBuilder.AddQuadFacing(rail, Lo(ai), Lo(bi), Hi(bi), Hi(ai), -outward);  // vertical inner lip
                MeshBuilder.AddQuadFacing(rail, Hi(ai), Hi(bi), Lo(b), Lo(a), Vector3.Up);  // slope falls outward
            }
        }

        if (!closed) CapEnds(outer, inset, h, BankCap);

        void BankCap(Vector2 o, Vector2 ins, Vector3 facing)
        {
            Vector3 Lo(Vector2 p) => new(p.X, 0, p.Y);
            Vector3 Hi(Vector2 p) => new(p.X, h, p.Y);
            // The wedge's cross-section triangle: lip side rises, slope side lies flat on the slab.
            if (!flip) MeshBuilder.AddTriFacing(rail, Lo(o), Hi(o), Lo(ins), facing);
            else       MeshBuilder.AddTriFacing(rail, Lo(ins), Hi(ins), Lo(o), facing);
        }
    }

    /// <summary>Caps the two open ends of a curb/bank run. Each end's cross-section is closed by
    /// <paramref name="cap"/>(outerVertex, insetVertex, outwardFacing), where the facing points along the run
    /// away from its interior (so the cap is the run's flat "cut" face).</summary>
    private static void CapEnds(Vector2[] outer, Vector2[] inset, float h, System.Action<Vector2, Vector2, Vector3> cap)
    {
        int n = outer.Length;
        Vector3 startDir = Dir(outer[0], outer[1]);            // run goes start → ...; cut faces back
        Vector3 endDir = Dir(outer[n - 2], outer[n - 1]);      // ... → end; cut faces forward
        cap(outer[0], inset[0], -startDir);
        cap(outer[n - 1], inset[n - 1], endDir);

        static Vector3 Dir(Vector2 a, Vector2 b)
        {
            var d = new Vector3(b.X - a.X, 0, b.Y - a.Y);
            return d.LengthSquared() > 1e-9f ? d.Normalized() : Vector3.Right;
        }
    }

    /// <summary>Elevated rail (fence): square posts at every vertex and at ~2.5 m intervals along each edge,
    /// plus a top beam running the outline at the rail height. Independent of the inset ring — posts/beam are
    /// oriented boxes centred on the outline. Post + beam cross-section = <paramref name="w"/>. An open run
    /// simply stops at the last vertex (its end posts cap it); a closed ring wraps.</summary>
    private static void BuildFence(SurfaceTool rail, Vector2[] outer, float h, float w, bool closed)
    {
        int n = outer.Length;
        int edges = closed ? n : n - 1;
        float half = w * 0.5f;
        const float spacing = 2.5f;

        void Post(Vector2 p)
            => AddOrientedBox(rail, new Vector3(p.X, h * 0.5f, p.Y),
                              new Vector3(half, 0, 0), new Vector3(0, h * 0.5f, 0), new Vector3(0, 0, half));

        for (int i = 0; i < n; i++) Post(outer[i]);  // a post at every vertex

        for (int i = 0; i < edges; i++)
        {
            Vector2 a = outer[i], b = outer[(i + 1) % n];
            float len = a.DistanceTo(b);
            if (len < 1e-4f) continue;

            int segs = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
            for (int s = 1; s < segs; s++) Post(a.Lerp(b, (float)s / segs)); // interior posts

            // Top beam along the edge: top flush at y=h, cross-section w×w, oriented along the edge.
            Vector2 dir = (b - a).Normalized();
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 mid = (a + b) * 0.5f;
            AddOrientedBox(rail, new Vector3(mid.X, h - half, mid.Y),
                           new Vector3(dir.X, 0, dir.Y) * (len * 0.5f),
                           new Vector3(0, half, 0),
                           new Vector3(perp.X, 0, perp.Y) * half);
        }
    }

    /// <summary>Adds a box from a centre and three half-extent axis vectors (any orientation). Each of the 6
    /// faces is emitted via <see cref="MeshBuilder.AddQuadFacing"/> toward its outward axis, so winding is
    /// auto-resolved.</summary>
    private static void AddOrientedBox(SurfaceTool st, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az)
    {
        Vector3 P(int sx, int sy, int sz) => c + ax * sx + ay * sy + az * sz;
        MeshBuilder.AddQuadFacing(st, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), ax);
        MeshBuilder.AddQuadFacing(st, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -ax);
        MeshBuilder.AddQuadFacing(st, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), ay);
        MeshBuilder.AddQuadFacing(st, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), -ay);
        MeshBuilder.AddQuadFacing(st, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), az);
        MeshBuilder.AddQuadFacing(st, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), -az);
    }

    /// <summary>Offsets a CLOSED outline inward by <paramref name="d"/> into a mitered inset ring: each edge's
    /// line is moved inward (toward <paramref name="centroid"/>) by d and consecutive lines intersected, so
    /// corners miter cleanly at any angle. Parallel/degenerate corners fall back to a straight inward vertex
    /// offset. Convex / gentle-concave only (a strongly concave corner could mis-orient) — fine for the angled
    /// playfields and convex discs this serves.</summary>
    private static Vector2[] InsetRingClosed(Vector2[] poly, Vector2 centroid, float d)
    {
        int n = poly.Length;
        var op = new Vector2[n];   // a point on each inward-offset edge line
        var od = new Vector2[n];   // its direction
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % n];
            Vector2 dir = b - a;
            if (dir.LengthSquared() < 1e-12f) { op[i] = a; od[i] = new Vector2(1, 0); continue; }
            dir = dir.Normalized();
            var nrm = new Vector2(-dir.Y, dir.X);
            Vector2 mid = (a + b) * 0.5f;
            if (nrm.Dot(centroid - mid) < 0) nrm = -nrm;  // orient inward (toward centroid)
            op[i] = a + nrm * d; od[i] = dir;
        }

        var res = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            int p = (i - 1 + n) % n;
            if (LineIntersect(op[p], od[p], op[i], od[i], out Vector2 x)) { res[i] = x; continue; }
            Vector2 toC = centroid - poly[i];   // parallel edges: offset the vertex straight inward
            res[i] = poly[i] + (toC.LengthSquared() > 1e-9f ? toC.Normalized() * d : Vector2.Zero);
        }
        return res;
    }

    /// <summary>Offsets an OPEN polyline inward by <paramref name="d"/>: interior vertices miter (intersect the
    /// two adjacent offset edge-lines), while the two END vertices offset perpendicular to their single edge
    /// (no neighbour to miter against). Inward is toward <paramref name="centroid"/>.</summary>
    private static Vector2[] InsetOpen(Vector2[] verts, Vector2 centroid, float d)
    {
        int n = verts.Length;
        int e = n - 1;             // edge i runs verts[i] → verts[i+1], i in 0..e-1
        var op = new Vector2[e];   // a point on each inward-offset edge line (at verts[i])
        var od = new Vector2[e];   // its direction
        var nm = new Vector2[e];   // its inward normal (unit)
        for (int i = 0; i < e; i++)
        {
            Vector2 a = verts[i], b = verts[i + 1];
            Vector2 dir = b - a;
            if (dir.LengthSquared() < 1e-12f) { op[i] = a; od[i] = new Vector2(1, 0); nm[i] = new Vector2(0, 1); continue; }
            dir = dir.Normalized();
            var nrm = new Vector2(-dir.Y, dir.X);
            Vector2 mid = (a + b) * 0.5f;
            if (nrm.Dot(centroid - mid) < 0) nrm = -nrm;  // orient inward (toward centroid)
            op[i] = a + nrm * d; od[i] = dir; nm[i] = nrm;
        }

        var res = new Vector2[n];
        res[0] = verts[0] + nm[0] * d;                 // start: perpendicular to the first edge
        res[n - 1] = verts[n - 1] + nm[e - 1] * d;     // end: perpendicular to the last edge
        for (int i = 1; i < n - 1; i++)
        {
            if (LineIntersect(op[i - 1], od[i - 1], op[i], od[i], out Vector2 x)) { res[i] = x; continue; }
            res[i] = verts[i] + nm[i] * d;             // parallel edges: straight perpendicular offset
        }
        return res;
    }

    /// <summary>Intersects two infinite lines (point + direction). False when near-parallel.</summary>
    private static bool LineIntersect(Vector2 p0, Vector2 d0, Vector2 p1, Vector2 d1, out Vector2 x)
    {
        float denom = d0.X * d1.Y - d0.Y * d1.X;
        if (Mathf.Abs(denom) < 1e-9f) { x = default; return false; }
        Vector2 diff = p1 - p0;
        float tt = (diff.X * d1.Y - diff.Y * d1.X) / denom;
        x = p0 + d0 * tt;
        return true;
    }

    /// <summary>Shortest edge length of the ring (closed) or polyline (open); ≥1 fallback when degenerate.</summary>
    private static float MinEdge(Vector2[] poly, bool closed)
    {
        int edges = closed ? poly.Length : poly.Length - 1;
        float min = float.MaxValue;
        for (int i = 0; i < edges; i++)
        {
            float len = poly[i].DistanceTo(poly[(i + 1) % poly.Length]);
            if (len > 1e-4f && len < min) min = len;
        }
        return min == float.MaxValue ? 1f : min;
    }
}
