using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A cross-section swept along a freeform path (the unified "path tool" — curved walls, banked roads,
/// half-pipe channels). The path is a list of control points stored in <c>Parameters["points"]</c>
/// (local space, relative to the instance origin); they are smoothed into a <see cref="Curve3D"/> at
/// build time and sampled into stations. At each station a rotation-minimizing frame (parallel
/// transport of an up vector) gives a (right, up) basis — so the cross-section keeps its orientation
/// through steep climbs and loops where a fixed world-up or a Frenet frame would twist or degenerate.
/// A global <c>bank</c> rolls the whole cross-section about the path tangent.
///
/// The cross-section is one of three <c>profile</c>s, each a closed 2D loop (frame coords: X = lateral,
/// Y = up) with a per-edge material slot, swept as quad strips + triangulated end caps:
///   0 Ribbon  — a flat slab you roll on (top = Surface, sides/bottom = Side); bank it for a banked road.
///   1 Channel — a U-shell half-pipe (concave inside = Surface, outer shell + rims = Side).
///   2 Wall    — a thin upright wall / guard rail (vertical faces = Surface, top/bottom = Side).
///
/// Loops are wound so the exterior normal is the edge's LEFT-normal (−dY, dX) — the same convention as
/// the verified <see cref="HalfPipePrimitive"/> sweep, so the quad-strip winding is inherited. End caps
/// triangulate the loop and orient each triangle by its geometric normal (TriangulatePolygon does not
/// guarantee a winding), so they can't silently invert.
/// </summary>
public sealed class PathSweepPrimitive : IPrimitive
{
    public string TypeId => "path_sweep";
    public string DisplayName => "Path Sweep";
    public string Category => "Curves";

    // Profile enum (the "profile" param).
    private const int Ribbon = 0, Channel = 1, Wall = 2;

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("profile",    "Profile (0=Ribbon 1=Channel 2=Wall)", ParamType.Int, 0, 0f, 2f),
        new ParamSpec("width",      "Width",       ParamType.Float, 4.0f,  0.1f, 1000f),
        new ParamSpec("thickness",  "Thickness",   ParamType.Float, 0.2f,  0.05f, 10f),
        new ParamSpec("bank",       "Bank (deg)",  ParamType.Float, 0.0f, -89f,  89f),
        new ParamSpec("wallHeight", "Wall Height", ParamType.Float, 3.0f,  0.1f, 100f),
        new ParamSpec("radius",     "Radius",      ParamType.Float, 1.5f,  0.1f, 100f),
        new ParamSpec("arc",        "Arc (deg)",   ParamType.Float, 180f,  30f,  270f),
        new ParamSpec("sides",      "Sides",       ParamType.Int,   12,    2f,   64f),
        new ParamSpec("segments",   "Segments",    ParamType.Int,   8,     1f,   64f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Surface", "Side" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        (List<Vector3> pts, List<float> banks) = ReadPath(data);
        var empty = new ArrayMesh();
        if (pts.Count < 2) return empty;

        int profile = GetI(data, "profile", Ribbon);
        float width = GetF(data, "width", 4f);
        float t = GetF(data, "thickness", 0.2f);
        float globalBank = GetF(data, "bank", 0f);
        float wallH = GetF(data, "wallHeight", 3f);
        float radius = GetF(data, "radius", 1.5f);
        float arc = Mathf.DegToRad(GetF(data, "arc", 180f));
        int sides = Mathf.Max(2, GetI(data, "sides", 12));
        int segPer = Mathf.Max(1, GetI(data, "segments", 8));

        // --- Path: smooth the control points into a Curve3D, sample evenly by baked length. ---
        Curve3D curve = BuildCurve(pts);
        float len = curve.GetBakedLength();
        if (len < 1e-4f) return empty;

        int stations = Mathf.Max(2, segPer * (pts.Count - 1));
        var pos = new Vector3[stations + 1];
        var dist = new float[stations + 1];
        for (int i = 0; i <= stations; i++)
        {
            dist[i] = len * i / stations;
            pos[i] = curve.SampleBaked(dist[i], true);
        }

        // --- Per-station tangent (central difference) + rotation-minimizing (right, up) frame. ---
        var T = new Vector3[stations + 1];
        for (int i = 0; i <= stations; i++)
        {
            Vector3 d = pos[Mathf.Min(stations, i + 1)] - pos[Mathf.Max(0, i - 1)];
            T[i] = d.LengthSquared() > 1e-9f ? d.Normalized() : (i > 0 ? T[i - 1] : Vector3.Right);
        }

        var R = new Vector3[stations + 1];
        var U = new Vector3[stations + 1];
        Vector3 seed = Mathf.Abs(T[0].Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
        U[0] = (seed - T[0] * seed.Dot(T[0])).Normalized();
        R[0] = T[0].Cross(U[0]).Normalized();
        for (int i = 1; i <= stations; i++)
        {
            // Parallel-transport the previous up by the minimal rotation taking T[i-1] onto T[i].
            Vector3 axis = T[i - 1].Cross(T[i]);
            float s = axis.Length(), c = T[i - 1].Dot(T[i]);
            Vector3 u = s > 1e-6f ? U[i - 1].Rotated(axis / s, Mathf.Atan2(s, c)) : U[i - 1];
            U[i] = (u - T[i] * u.Dot(T[i])).Normalized();
            R[i] = T[i].Cross(U[i]).Normalized();
        }

        // Bank: roll the frame about the tangent (preserves orthonormality) by the global bank plus the
        // per-point bank interpolated by arc length. Control-point arc offsets come from the baked curve
        // (GetClosestOffset), pinned at the ends, so each station blends its bracketing points' banks.
        var cpOff = new float[pts.Count];
        for (int k = 0; k < pts.Count; k++) cpOff[k] = curve.GetClosestOffset(pts[k]);
        cpOff[0] = 0f;
        cpOff[pts.Count - 1] = len;
        for (int i = 0; i <= stations; i++)
        {
            float bankDeg = globalBank + BankAt(dist[i], cpOff, banks);
            if (Mathf.Abs(bankDeg) < 1e-4f) continue;
            float a = Mathf.DegToRad(bankDeg);
            R[i] = R[i].Rotated(T[i], a);
            U[i] = U[i].Rotated(T[i], a);
        }

        // --- Cross-section loop + per-edge slot for the chosen profile. ---
        (Vector2[] loop, int[] slot) = profile switch
        {
            Channel => ChannelLoop(radius, t, arc, sides),
            Wall    => WallLoop(t, wallH),
            _       => RibbonLoop(width, t),
        };

        SurfaceTool surface = Begin(), side = Begin();
        Sweep(surface, side, pos, R, U, T, dist, loop, slot);

        var mesh = new ArrayMesh();
        Commit(surface, mesh);
        Commit(side, mesh);
        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
    {
        ArrayMesh mesh = BuildMesh(data, ctx);
        if (mesh.GetSurfaceCount() == 0) return System.Array.Empty<Shape3D>();
        return new Shape3D[] { mesh.CreateTrimeshShape() };
    }

    // --- Cross-section profiles. Frame coords: X = lateral (right), Y = up. Wound exterior-on-left
    // (outward normal = the edge's left-normal (−dY, dX)). slot[e] routes edge e: 0 = Surface, 1 = Side.

    private static (Vector2[], int[]) RibbonLoop(float w, float t)
    {
        float h = w * 0.5f;
        var loop = new[] { new Vector2(-h, 0), new Vector2(h, 0), new Vector2(h, -t), new Vector2(-h, -t) };
        return (loop, new[] { 0, 1, 1, 1 }); // top = Surface, right/bottom/left = Side
    }

    private static (Vector2[], int[]) WallLoop(float t, float h)
    {
        float hx = t * 0.5f;
        var loop = new[] { new Vector2(-hx, h), new Vector2(hx, h), new Vector2(hx, 0), new Vector2(-hx, 0) };
        return (loop, new[] { 1, 0, 1, 0 }); // top = Side, right/left vertical = Surface, bottom = Side
    }

    private static (Vector2[], int[]) ChannelLoop(float r, float t, float arc, int sides)
    {
        float half = arc * 0.5f;
        int count = 2 * (sides + 1);
        var loop = new Vector2[count];
        var slot = new int[count];
        // Inner concave arc (Surface), left rim → bottom → right rim.
        for (int k = 0; k <= sides; k++)
        {
            float phi = -half + arc * k / sides;
            loop[k] = new Vector2(r * Mathf.Sin(phi), r - r * Mathf.Cos(phi));
        }
        // Outer shell arc (Side), right rim → bottom → left rim.
        for (int k = 0; k <= sides; k++)
        {
            float phi = half - arc * k / sides;
            loop[sides + 1 + k] = new Vector2((r + t) * Mathf.Sin(phi), r - (r + t) * Mathf.Cos(phi));
        }
        // First `sides` edges are the inner arc (Surface); the rims + outer arc are Side.
        for (int e = 0; e < count; e++) slot[e] = e < sides ? 0 : 1;
        return (loop, slot);
    }

    // --- Sweep the closed loop along the stations: one quad strip per edge + triangulated end caps. ---

    private static void Sweep(SurfaceTool surface, SurfaceTool side, Vector3[] pos, Vector3[] R, Vector3[] U,
        Vector3[] T, float[] dist, Vector2[] loop, int[] slot)
    {
        int stations = pos.Length - 1;
        int n = loop.Length;

        var perim = new float[n + 1]; // V = cumulative loop perimeter (world-unit UVs)
        for (int e = 0; e < n; e++) perim[e + 1] = perim[e] + loop[e].DistanceTo(loop[(e + 1) % n]);

        Vector3 Frame(int i, Vector2 q) => pos[i] + q.X * R[i] + q.Y * U[i];

        for (int e = 0; e < n; e++)
        {
            Vector2 pe = loop[e], pf = loop[(e + 1) % n];
            Vector2 d = pf - pe;
            var ln = new Vector2(-d.Y, d.X);
            if (ln.LengthSquared() < 1e-12f) continue; // degenerate edge (e.g. a closed-tube rim)
            ln = ln.Normalized();
            SurfaceTool st = slot[e] == 0 ? surface : side;
            float vE = perim[e], vF = perim[e + 1];
            for (int i = 0; i < stations; i++)
            {
                int j = i + 1;
                Vector3 nrm = (ln.X * R[i] + ln.Y * U[i]).Normalized();
                MeshBuilder.AddQuad(st, Frame(i, pe), Frame(i, pf), Frame(j, pf), Frame(j, pe), nrm,
                    new Vector2(dist[i], vE), new Vector2(dist[i], vF),
                    new Vector2(dist[j], vF), new Vector2(dist[j], vE));
            }
        }

        AddCap(side, loop, 0, pos, R, U, -T[0]);
        AddCap(side, loop, stations, pos, R, U, T[stations]);
    }

    /// <summary>
    /// One end cap: triangulate the cross-section loop in frame 2D coords and emit it at station
    /// <paramref name="i"/>. TriangulatePolygon's winding isn't guaranteed, so each triangle is oriented
    /// by its own geometric normal toward <paramref name="refN"/> (the outward face direction).
    /// </summary>
    private static void AddCap(SurfaceTool side, Vector2[] loop, int i, Vector3[] pos, Vector3[] R, Vector3[] U,
        Vector3 refN)
    {
        int[] tris = Geometry2D.TriangulatePolygon(loop);
        if (tris == null || tris.Length < 3) return;
        Vector3 P(Vector2 q) => pos[i] + q.X * R[i] + q.Y * U[i];
        for (int k = 0; k + 2 < tris.Length; k += 3)
        {
            Vector2 a2 = loop[tris[k]], b2 = loop[tris[k + 1]], c2 = loop[tris[k + 2]];
            Vector3 a = P(a2), b = P(b2), c = P(c2);
            Vector3 nrm = (b - a).Cross(c - a);
            if (nrm.LengthSquared() < 1e-12f) continue;
            nrm = nrm.Normalized();
            if (nrm.Dot(refN) >= 0) MeshBuilder.AddTri(side, a, b, c, nrm, a2, b2, c2);
            else MeshBuilder.AddTri(side, a, c, b, -nrm, a2, c2, b2);
        }
    }

    // --- Path smoothing: Catmull-Rom-style Bezier handles on a Curve3D. ---

    private static Curve3D BuildCurve(List<Vector3> pts)
    {
        var curve = new Curve3D { BakeInterval = 0.05f };
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = pts[Mathf.Max(0, i - 1)];
            Vector3 next = pts[Mathf.Min(n - 1, i + 1)];
            Vector3 dir = next - prev;
            dir = dir.LengthSquared() > 1e-9f ? dir.Normalized() : Vector3.Right;
            float inLen = (i > 0 ? pts[i].DistanceTo(pts[i - 1]) : 0f) / 3f;
            float outLen = (i < n - 1 ? pts[i].DistanceTo(pts[i + 1]) : 0f) / 3f;
            curve.AddPoint(pts[i], -dir * inLen, dir * outLen);
        }
        return curve;
    }

    /// <summary>Reads control points and their parallel per-point bank (degrees), dropping near-coincident
    /// consecutive points (zero-length segments produce NaN tangents — the live preview's hovered point is
    /// often a duplicate of the last) and their banks in lockstep so the two lists stay index-aligned.</summary>
    private static (List<Vector3>, List<float>) ReadPath(PrimitiveInstanceData data)
    {
        var pts = new List<Vector3>();
        var banks = new List<float>();
        if (!data.Parameters.ContainsKey("points")) return (pts, banks);

        Godot.Collections.Array rawPts = data.Parameters["points"].AsGodotArray();
        Godot.Collections.Array rawBanks = data.Parameters.ContainsKey("banks")
            ? data.Parameters["banks"].AsGodotArray() : new Godot.Collections.Array();
        for (int i = 0; i < rawPts.Count; i++)
        {
            Vector3 p = rawPts[i].AsVector3();
            if (pts.Count > 0 && p.DistanceTo(pts[^1]) <= 1e-3f) continue;
            pts.Add(p);
            banks.Add(i < rawBanks.Count ? rawBanks[i].AsSingle() : 0f);
        }
        return (pts, banks);
    }

    /// <summary>Per-point bank (degrees) interpolated at baked offset <paramref name="d"/>: find the
    /// control-point interval containing it and lerp the bracketing banks.</summary>
    private static float BankAt(float d, float[] cpOff, List<float> banks)
    {
        int n = banks.Count;
        if (n == 0) return 0f;
        if (n == 1) return banks[0];
        for (int k = 0; k < n - 1; k++)
        {
            if (d > cpOff[k + 1] && k < n - 2) continue;
            float seg = cpOff[k + 1] - cpOff[k];
            float tt = seg > 1e-5f ? Mathf.Clamp((d - cpOff[k]) / seg, 0f, 1f) : 0f;
            return Mathf.Lerp(banks[k], banks[k + 1], tt);
        }
        return banks[n - 1];
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

    private static int GetI(PrimitiveInstanceData d, string key, int def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsInt32() : def;
}
