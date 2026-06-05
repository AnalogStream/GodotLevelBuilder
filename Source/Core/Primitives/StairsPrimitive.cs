using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A straight, solid staircase of <c>steps</c> equal steps climbing <c>totalRise</c> over <c>run</c>,
/// the full <c>width</c>. Local space is centred on X (the run) and Z (width) with the base at y=0,
/// so X(u)=u-run/2 and the flight ascends along local +X.
///
/// Built by silhouette decomposition (no CSG): step i contributes a riser quad (its vertical front)
/// and a tread quad (its horizontal top); the solid under the flight shows as a stepped side
/// silhouette tiled from per-step column quads, plus a vertical back and a bottom. Surfaces:
/// 0 Tread, 1 Riser, 2 Side (sides + back + bottom). The side columns use position-based UVs so the
/// texture stays continuous up the flight (same trick the wall uses across openings).
/// </summary>
public sealed class StairsPrimitive : IPrimitive
{
    public string TypeId => "stairs";
    public string DisplayName => "Stairs";
    public string Category => "Vertical";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("steps",     "Steps",      ParamType.Int,   12,    1f,    100f),
        new ParamSpec("totalRise", "Total Rise", ParamType.Float, 3.0f,  0.05f, 100f),
        new ParamSpec("run",       "Run",        ParamType.Float, 3.0f,  0.1f,  1000f),
        new ParamSpec("width",     "Width",      ParamType.Float, 1.2f,  0.1f,  100f),
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

        float X(float u) => u - run * 0.5f;

        SurfaceTool treads = Begin(), risers = Begin(), side = Begin();

        for (int i = 0; i < n; i++)
        {
            float u0 = i * tread, u1 = (i + 1) * tread;
            float yBot = i * riser, yTop = (i + 1) * riser;
            float x0 = X(u0), x1 = X(u1);

            // Riser front (faces −X, toward the climber): vertical face at the step's front edge.
            MeshBuilder.AddQuad(risers,
                new(x0, yBot, zl), new(x0, yBot, zr), new(x0, yTop, zr), new(x0, yTop, zl), new Vector3(-1, 0, 0));

            // Tread top (faces +Y): horizontal step surface.
            MeshBuilder.AddQuad(treads,
                new(x0, yTop, zl), new(x0, yTop, zr), new(x1, yTop, zr), new(x1, yTop, zl), Vector3.Up);

            // Side columns under this tread, one per side. Position-based UVs (U along run, V from top)
            // so the stepped silhouette tiles continuously up the flight.
            MeshBuilder.AddQuad(side,
                new(x1, 0, zl), new(x0, 0, zl), new(x0, yTop, zl), new(x1, yTop, zl), new Vector3(0, 0, -1),
                new Vector2(run - u1, rise), new Vector2(run - u0, rise), new Vector2(run - u0, rise - yTop), new Vector2(run - u1, rise - yTop));
            MeshBuilder.AddQuad(side,
                new(x0, 0, zr), new(x1, 0, zr), new(x1, yTop, zr), new(x0, yTop, zr), new Vector3(0, 0, 1),
                new Vector2(u0, rise), new Vector2(u1, rise), new Vector2(u1, rise - yTop), new Vector2(u0, rise - yTop));
        }

        // Vertical back at the top of the flight (faces +X) and the bottom (faces −Y).
        float xb = X(run), xf = X(0f);
        MeshBuilder.AddQuad(side, new(xb, 0, zr), new(xb, 0, zl), new(xb, rise, zl), new(xb, rise, zr), new Vector3(1, 0, 0));
        MeshBuilder.AddQuad(side, new(xf, 0, zl), new(xb, 0, zl), new(xb, 0, zr), new(xf, 0, zr), Vector3.Down);

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
