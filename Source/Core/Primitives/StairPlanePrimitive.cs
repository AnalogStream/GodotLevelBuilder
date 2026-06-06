using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A staircase with no solid base: a constant-thickness folded plate following the step silhouette —
/// the "thick stepped plane" counterpart to <see cref="StairsPrimitive"/>. The top is the same
/// tread/riser silhouette climbing <c>totalRise</c> over <c>run</c> in <c>steps</c> equal steps; the
/// underside is that silhouette offset by <c>thickness</c> perpendicular to each face, which (because
/// treads are horizontal and risers vertical) is exactly the top profile translated by (+t, −t).
///
/// That translate only yields a clean, non-self-intersecting plate while <c>t &lt; min(tread, riser)</c>,
/// so <see cref="BuildMesh"/> clamps thickness to 95% of that minimum — the static ParamSpec max can't,
/// since tread/riser depend on run/steps. Local space is centred on X (run) and Z (width); X(u)=u-run/2
/// and the flight ascends along local +X. Surfaces: 0 Tread, 1 Riser, 2 Side (underside, back of each
/// riser, the two stepped side caps and the front/top end caps).
/// </summary>
public sealed class StairPlanePrimitive : IPrimitive
{
    public string TypeId => "stair_plane";
    public string DisplayName => "Stair Plane";
    public string Category => "Vertical";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("steps",     "Steps",      ParamType.Int,   12,    1f,    100f),
        new ParamSpec("totalRise", "Total Rise", ParamType.Float, 3.0f,  0.05f, 100f),
        new ParamSpec("run",       "Run",        ParamType.Float, 3.0f,  0.1f,  1000f),
        new ParamSpec("width",     "Width",      ParamType.Float, 1.2f,  0.1f,  100f),
        new ParamSpec("thickness", "Thickness",  ParamType.Float, 0.1f,  0.01f, 50f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Tread", "Riser", "Side" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        int n = Mathf.Max(1, GetI(data, "steps", 12));
        float rise = GetF(data, "totalRise", 3f);
        float run = GetF(data, "run", 3f);
        float w = GetF(data, "width", 1.2f);
        float zl = -w * 0.5f, zr = w * 0.5f;
        float tread = run / n, riser = rise / n;
        // Clamp so the (+t, −t) underside never crosses its own step → no self-intersection.
        float t = Mathf.Min(GetF(data, "thickness", 0.1f), 0.95f * Mathf.Min(tread, riser));

        float X(float u) => u - run * 0.5f;

        SurfaceTool treads = Begin(), risers = Begin(), side = Begin();

        // One side-cap quad per profile segment (z=zl outward −Z, z=zr outward +Z). The band slice
        // between a top edge V0→V1 and its (+t,−t)-translated underside edge is a planar parallelogram.
        void SideSegment(Vector3 v0, Vector3 v1)
        {
            Vector3 v0b = v0 + new Vector3(t, -t, 0), v1b = v1 + new Vector3(t, -t, 0);
            MeshBuilder.AddQuad(side,
                new(v0.X, v0.Y, zl), new(v1.X, v1.Y, zl), new(v1b.X, v1b.Y, zl), new(v0b.X, v0b.Y, zl),
                new Vector3(0, 0, -1));
            MeshBuilder.AddQuad(side,
                new(v0b.X, v0b.Y, zr), new(v1b.X, v1b.Y, zr), new(v1.X, v1.Y, zr), new(v0.X, v0.Y, zr),
                new Vector3(0, 0, 1));
        }

        for (int i = 0; i < n; i++)
        {
            float u0 = i * tread, u1 = (i + 1) * tread;
            float yBot = i * riser, yTop = (i + 1) * riser;
            float x0 = X(u0), x1 = X(u1);
            float xb = x0 + t, ybB = yBot - t, ytB = yTop - t; // underside (back/down) coordinates

            // Riser front (faces −X) and its under/back face (faces +X).
            MeshBuilder.AddQuad(risers,
                new(x0, yBot, zl), new(x0, yBot, zr), new(x0, yTop, zr), new(x0, yTop, zl), new Vector3(-1, 0, 0));
            MeshBuilder.AddQuad(side,
                new(xb, ybB, zl), new(xb, ytB, zl), new(xb, ytB, zr), new(xb, ybB, zr), new Vector3(1, 0, 0));

            // Tread top (faces +Y) and its underside (faces −Y).
            MeshBuilder.AddQuad(treads,
                new(x0, yTop, zl), new(x0, yTop, zr), new(x1, yTop, zr), new(x1, yTop, zl), Vector3.Up);
            MeshBuilder.AddQuad(side,
                new(xb, ytB, zl), new(x1 + t, ytB, zl), new(x1 + t, ytB, zr), new(xb, ytB, zr), Vector3.Down);

            // Side caps: the riser segment then the tread segment of the stepped silhouette.
            SideSegment(new(x0, yBot, 0), new(x0, yTop, 0));
            SideSegment(new(x0, yTop, 0), new(x1, yTop, 0));
        }

        // Front end cap (foot) and back end cap (top landing edge): the short (+t,−t) edges, extruded
        // across the width.
        float xf = X(0f), xt = X(run);
        MeshBuilder.AddQuad(side,
            new(xf, 0, zl), new(xf + t, -t, zl), new(xf + t, -t, zr), new(xf, 0, zr),
            new Vector3(-1, -1, 0).Normalized());
        MeshBuilder.AddQuad(side,
            new(xt, rise, zr), new(xt + t, rise - t, zr), new(xt + t, rise - t, zl), new(xt, rise, zl),
            new Vector3(1, 1, 0).Normalized());

        var mesh = new ArrayMesh();
        Commit(treads, mesh);
        Commit(risers, mesh);
        Commit(side, mesh);
        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
        => new Shape3D[] { BuildMesh(data, ctx).CreateTrimeshShape() };

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
