using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A flat circular slab — the round counterpart of <see cref="PolygonFloorPrimitive"/>. A disc of
/// <c>radius</c> tessellated into <c>sides</c> facets, top face at local y=0, extending down by
/// <c>thickness</c>. Surfaces: 0 Top (+Y), 1 Bottom (−Y), 2 Edge (the cylindrical side wall), 3 Rail.
///
/// The auto-rail follows the rim, sharing <see cref="RailBuilder"/> with the polygon floor (None / Rail /
/// Elevated Rail / Bank, value-stable). The circle outline is a regular polygon, so the closed-ring rail
/// path applies directly — the rim is just a many-sided convex ring. "Rail" is a TRAILING, CONDITIONAL slot
/// (surface 3 emitted only when rail != None), so the positional slot mapping for Top/Bottom/Edge stays
/// stable whether or not a rail is present (CLAUDE.md gotcha #3).
/// </summary>
public sealed class CirclePlanePrimitive : IPrimitive
{
    public string TypeId => "circle_plane";
    public string DisplayName => "Circle Plane";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("radius",    "Radius",    ParamType.Float, 2.0f, 0.1f, 100f),
        new ParamSpec("sides",     "Sides",     ParamType.Int,   32,   3f,   256f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 100f),
        // Auto-rail along the rim (same four styles + Height/Width/Bank Angle as the polygon floor). Append-only.
        new ParamSpec("rail",       "Rail",        ParamType.Int,   0, 0f, 3f, new[] { "None", "Rail", "Elevated Rail", "Bank" }),
        new ParamSpec("railHeight", "Rail Height", ParamType.Float, 0.4f, 0.02f, 50f),
        new ParamSpec("railWidth",  "Rail Width",  ParamType.Float, 0.2f, 0.02f, 50f),
        new ParamSpec("bankAngle",  "Bank Angle",  ParamType.Float, 45f, -85f, 85f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Top", "Bottom", "Edge", "Rail" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        var mesh = new ArrayMesh();
        float r = GetF(data, "radius", 2f);
        int sides = Mathf.Max(3, GetI(data, "sides", 32));
        float t = GetF(data, "thickness", 0.2f);
        if (r < 1e-4f) return mesh;

        // Rim ring (XZ). circle2d feeds the rail; the 3D points build the caps + side wall.
        var rim = new Vector3[sides];
        var circle2d = new Vector2[sides];
        for (int k = 0; k < sides; k++)
        {
            float a = Mathf.Tau * k / sides;
            float x = r * Mathf.Cos(a), z = r * Mathf.Sin(a);
            rim[k] = new Vector3(x, 0, z);
            circle2d[k] = new Vector2(x, z);
        }

        // Surface 0/1: top + bottom caps, fanned from the centre (the disc is convex, so a fan is exact).
        SurfaceTool top = Begin(), bottom = Begin();
        var topC = new Vector3(0, 0, 0);
        var botC = new Vector3(0, -t, 0);
        for (int k = 0; k < sides; k++)
        {
            int m = (k + 1) % sides;
            MeshBuilder.AddTriFacing(top, topC, rim[k], rim[m], Vector3.Up);
            var b0 = new Vector3(rim[k].X, -t, rim[k].Z);
            var b1 = new Vector3(rim[m].X, -t, rim[m].Z);
            MeshBuilder.AddTriFacing(bottom, botC, b0, b1, Vector3.Down);
        }
        Commit(top, mesh);
        Commit(bottom, mesh);

        // Surface 2: Edge — the cylindrical side wall, one vertical quad per facet (faces outward).
        SurfaceTool edge = Begin();
        for (int k = 0; k < sides; k++)
        {
            int m = (k + 1) % sides;
            var tk = rim[k]; var tm = rim[m];
            var bm = new Vector3(tm.X, -t, tm.Z);
            var bk = new Vector3(tk.X, -t, tk.Z);
            Vector3 outward = ((tk + tm) * 0.5f); outward.Y = 0;
            MeshBuilder.AddQuadFacing(edge, tk, tm, bm, bk, outward);
        }
        Commit(edge, mesh);

        // Surface 3: Rail — trailing-conditional rim rail, inset toward the centre.
        int railStyle = GetI(data, "rail", 0);
        if (railStyle != 0)
        {
            SurfaceTool rail = Begin();
            // maxInset = 0.45×radius: the rim can inset radially up to that without the disc inverting — the
            // facet-length default would over-clamp a finely-tessellated circle to a sliver.
            if (RailBuilder.EmitRing(rail, circle2d, Vector2.Zero, railStyle,
                                     GetF(data, "railHeight", 0.4f), GetF(data, "railWidth", 0.2f),
                                     GetF(data, "bankAngle", 45f), inward: true, maxInset: 0.45f * r))
                Commit(rail, mesh);
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
