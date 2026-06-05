using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A wedge ramp: a walkable surface sloping from y=0 at the front (u=0) up to <c>rise</c> at the
/// back (u=length), spanning the full width. Local space is centred on X (the run) and Z (width)
/// with the base at y=0, so X(u)=u-length/2 and the run ascends along local +X.
///
/// Surfaces: 0 Surface (the sloped top), 1 Side (bottom, vertical back, and the two triangular
/// sides). Corners are passed CCW-as-seen-from-the-normal so the V-flipped UVs come out upright.
/// </summary>
public sealed class RampPrimitive : IPrimitive
{
    public string TypeId => "ramp";
    public string DisplayName => "Ramp";
    public string Category => "Vertical";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("length", "Length", ParamType.Float, 3.0f, 0.1f, 1000f),
        new ParamSpec("rise",   "Rise",   ParamType.Float, 3.0f, 0.05f, 100f),
        new ParamSpec("width",  "Width",  ParamType.Float, 1.2f, 0.1f, 100f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Surface", "Side" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float l = GetF(data, "length", 3f);
        float r = GetF(data, "rise", 3f);
        float w = GetF(data, "width", 1.2f);
        float zl = -w * 0.5f, zr = w * 0.5f;
        float x0 = -l * 0.5f, x1 = l * 0.5f; // front (u=0, y=0) and back (u=l, y=r)

        SurfaceTool surface = Begin(), side = Begin();

        // Sloped walkable top: front-bottom up to back-top, normal pointing up-and-toward-front.
        MeshBuilder.AddQuad(surface,
            new(x0, 0, zl), new(x0, 0, zr), new(x1, r, zr), new(x1, r, zl),
            new Vector3(-r, l, 0).Normalized());

        // Bottom (faces down).
        MeshBuilder.AddQuad(side, new(x0, 0, zr), new(x0, 0, zl), new(x1, 0, zl), new(x1, 0, zr), Vector3.Down);
        // Vertical back face at the high end (faces +X).
        MeshBuilder.AddQuad(side, new(x1, 0, zr), new(x1, 0, zl), new(x1, r, zl), new(x1, r, zr), new Vector3(1, 0, 0));

        // Two triangular sides. UVs: U along the run (x + l/2), V = height-from-top (r − y), matching
        // the V-flip convention so the grid reads upright.
        MeshBuilder.AddTri(side, new(x0, 0, zl), new(x1, r, zl), new(x1, 0, zl), new Vector3(0, 0, -1),
            new Vector2(0, r), new Vector2(l, 0), new Vector2(l, r));   // left side (−Z)
        MeshBuilder.AddTri(side, new(x0, 0, zr), new(x1, 0, zr), new(x1, r, zr), new Vector3(0, 0, 1),
            new Vector2(0, r), new Vector2(l, r), new Vector2(l, 0));   // right side (+Z)

        var mesh = new ArrayMesh();
        Commit(surface, mesh);
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
}
