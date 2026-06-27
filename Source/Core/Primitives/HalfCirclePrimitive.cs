using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A flat half-disc slab — half of <see cref="CirclePlanePrimitive"/>. The diameter runs along local X
/// (from (−R,0,0) to (R,0,0)) and the arc bulges toward local +Z; the draw tool rotates the instance so the
/// bulge points the way you dragged. Top face at local y=0, extending down by <c>thickness</c>. Surfaces:
/// 0 Top (+Y), 1 Bottom (−Y), 2 Edge (the arc wall + the straight diameter wall), 3 Rail.
///
/// Two INDEPENDENT auto-rails feed the single trailing-conditional "Rail" surface (CLAUDE.md gotcha #3 — one
/// conditional surface keeps Top/Bottom/Edge's positional mapping stable): the ARC rail follows the curved rim
/// (<c>rail</c>…) and the optional STRAIGHT rail runs the diameter (<c>straightRail</c>…). Both are OPEN runs,
/// so they use <see cref="RailBuilder.EmitOpen"/> (capped ends) rather than the closed-ring path the full
/// circle / polygon use. Same four styles as the polygon floor (None / Rail / Elevated Rail / Bank).
/// </summary>
public sealed class HalfCirclePrimitive : IPrimitive
{
    public string TypeId => "half_circle";
    public string DisplayName => "Half Circle";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("radius",    "Radius",    ParamType.Float, 2.0f, 0.1f, 100f),
        new ParamSpec("sides",     "Sides",     ParamType.Int,   24,   2f,   256f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 100f),
        // Arc rail along the curved rim (same four styles + Height/Width/Bank Angle). Append-only enum.
        new ParamSpec("rail",       "Arc Rail",    ParamType.Int,   0, 0f, 3f, new[] { "None", "Rail", "Elevated Rail", "Bank" }),
        new ParamSpec("railHeight", "Arc Rail Height", ParamType.Float, 0.4f, 0.02f, 50f),
        new ParamSpec("railWidth",  "Arc Rail Width",  ParamType.Float, 0.2f, 0.02f, 50f),
        new ParamSpec("bankAngle",  "Arc Bank Angle",  ParamType.Float, 45f, -85f, 85f),
        // Optional independent rail along the straight (diameter) edge — off by default.
        new ParamSpec("straightRail",       "Straight Rail",        ParamType.Int,   0, 0f, 3f, new[] { "None", "Rail", "Elevated Rail", "Bank" }),
        new ParamSpec("straightRailHeight", "Straight Rail Height", ParamType.Float, 0.4f, 0.02f, 50f),
        new ParamSpec("straightRailWidth",  "Straight Rail Width",  ParamType.Float, 0.2f, 0.02f, 50f),
        new ParamSpec("straightBankAngle",  "Straight Bank Angle",  ParamType.Float, 45f, -85f, 85f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Top", "Bottom", "Edge", "Rail" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        var mesh = new ArrayMesh();
        float r = GetF(data, "radius", 2f);
        int sides = Mathf.Max(2, GetI(data, "sides", 24));
        float t = GetF(data, "thickness", 0.2f);
        if (r < 1e-4f) return mesh;

        // Arc points: θ from 0 (at +X) to π (at −X), bulging through +Z. sides+1 points, sides arc edges.
        var arc = new Vector3[sides + 1];
        var arc2d = new Vector2[sides + 1];
        for (int k = 0; k <= sides; k++)
        {
            float a = Mathf.Pi * k / sides;
            float x = r * Mathf.Cos(a), z = r * Mathf.Sin(a);
            arc[k] = new Vector3(x, 0, z);
            arc2d[k] = new Vector2(x, z);
        }
        // Half-disc centroid sits at z = 4R/3π on the +Z side — the "inward" reference for both rails.
        var centroid2d = new Vector2(0f, 4f * r / (3f * Mathf.Pi));

        // Surface 0/1: top + bottom caps, fanned from the diameter midpoint (origin). The half-disc is convex,
        // so the fan over the arc edges tiles it exactly.
        SurfaceTool top = Begin(), bottom = Begin();
        var topC = new Vector3(0, 0, 0);
        var botC = new Vector3(0, -t, 0);
        for (int k = 0; k < sides; k++)
        {
            MeshBuilder.AddTriFacing(top, topC, arc[k], arc[k + 1], Vector3.Up);
            var b0 = new Vector3(arc[k].X, -t, arc[k].Z);
            var b1 = new Vector3(arc[k + 1].X, -t, arc[k + 1].Z);
            MeshBuilder.AddTriFacing(bottom, botC, b0, b1, Vector3.Down);
        }
        Commit(top, mesh);
        Commit(bottom, mesh);

        // Surface 2: Edge — arc wall (one vertical quad per arc facet, faces outward) + the straight diameter
        // wall (one quad from (−R) to (+R), faces −Z away from the centroid).
        SurfaceTool edge = Begin();
        for (int k = 0; k < sides; k++)
        {
            Vector3 tk = arc[k], tm = arc[k + 1];
            var bm = new Vector3(tm.X, -t, tm.Z);
            var bk = new Vector3(tk.X, -t, tk.Z);
            Vector3 outward = (tk + tm) * 0.5f; outward.Y = 0;
            MeshBuilder.AddQuadFacing(edge, tk, tm, bm, bk, outward);
        }
        Vector3 dA = arc[sides], dB = arc[0];                 // (−R,0,0) → (R,0,0)
        MeshBuilder.AddQuadFacing(edge, dA, dB, new Vector3(dB.X, -t, dB.Z), new Vector3(dA.X, -t, dA.Z),
                                  new Vector3(0, 0, -1));
        Commit(edge, mesh);

        // Surface 3: Rail — the arc rail and the optional straight rail share this one trailing-conditional
        // surface (emitted when EITHER is non-None), both open runs inset toward the centroid.
        int arcStyle = GetI(data, "rail", 0);
        int straightStyle = GetI(data, "straightRail", 0);
        if (arcStyle != 0 || straightStyle != 0)
        {
            SurfaceTool rail = Begin();
            bool any = false;

            // Arc rail insets radially: bound by 0.45×radius, not the tiny facet length (which would over-clamp).
            if (arcStyle != 0)
                any |= RailBuilder.EmitOpen(rail, arc2d, centroid2d, arcStyle,
                                            GetF(data, "railHeight", 0.4f), GetF(data, "railWidth", 0.2f),
                                            GetF(data, "bankAngle", 45f), maxInset: 0.45f * r);

            if (straightStyle != 0)
            {
                var straight2d = new[] { new Vector2(-r, 0f), new Vector2(r, 0f) };
                any |= RailBuilder.EmitOpen(rail, straight2d, centroid2d, straightStyle,
                                            GetF(data, "straightRailHeight", 0.4f), GetF(data, "straightRailWidth", 0.2f),
                                            GetF(data, "straightBankAngle", 45f));
            }

            if (any) Commit(rail, mesh);
        }

        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
    {
        ArrayMesh mesh = BuildMesh(data, ctx);
        if (mesh.GetSurfaceCount() == 0) return System.Array.Empty<Shape3D>();
        return new Shape3D[] { mesh.CreateTrimeshShape() };
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
