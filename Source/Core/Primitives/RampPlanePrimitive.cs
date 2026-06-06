using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A ramp with no solid base: a constant-thickness slab tilted along the run — a "thick plane that
/// goes diagonally". The walkable top is the same sloped surface as <see cref="RampPrimitive"/>
/// (y=0 at the front, <c>rise</c> at the back, over <c>length</c>); the underside is that surface
/// offset by <c>thickness</c> measured perpendicular to the slope, so the slab is a true tilted
/// board (a parallelepiped) rather than a filled wedge.
///
/// Local space is centred on X (run) and Z (width) with the top-front edge at y=0, so X(u)=u-length/2
/// and the run ascends along local +X. Surfaces: 0 Surface (the sloped top), 1 Side (underside, the
/// front/back end caps and the two side faces). Corners are passed CCW-as-seen-from-the-normal.
/// </summary>
public sealed class RampPlanePrimitive : IPrimitive
{
    public string TypeId => "ramp_plane";
    public string DisplayName => "Ramp Plane";
    public string Category => "Vertical";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("length",    "Length",    ParamType.Float, 3.0f, 0.1f,  1000f),
        new ParamSpec("rise",      "Rise",      ParamType.Float, 3.0f, 0.05f, 100f),
        new ParamSpec("width",     "Width",     ParamType.Float, 1.2f, 0.1f,  100f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 50f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Surface", "Side" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float l = GetF(data, "length", 3f);
        float r = GetF(data, "rise", 3f);
        float w = GetF(data, "width", 1.2f);
        float t = GetF(data, "thickness", 0.2f);
        float zl = -w * 0.5f, zr = w * 0.5f;
        float x0 = -l * 0.5f, x1 = l * 0.5f; // front (top edge at y=0) and back (top edge at y=r)

        // Perpendicular slab offset: top normal is (-r, l, 0)/Ls (up-and-toward-front), so the
        // underside sits a distance t down that normal, i.e. along (r, -l, 0)/Ls.
        float ls = Mathf.Sqrt(l * l + r * r);
        var offset = new Vector3(r, -l, 0) / ls * t;
        var topN = new Vector3(-r, l, 0) / ls;
        var botN = -topN;

        // Top corners (walkable surface).
        Vector3 tlf = new(x0, 0, zl), trf = new(x0, 0, zr), trb = new(x1, r, zr), tlb = new(x1, r, zl);
        // Bottom corners (top shifted along the slab's downward normal).
        Vector3 blf = tlf + offset, brf = trf + offset, brb = trb + offset, blb = tlb + offset;

        SurfaceTool surface = Begin(), side = Begin();

        // Sloped walkable top.
        MeshBuilder.AddQuad(surface, tlf, trf, trb, tlb, topN);

        // Underside (parallel to the top, faces down the slope normal). Reverse winding of the top.
        MeshBuilder.AddQuad(side, blf, blb, brb, brf, botN);

        // Front end cap (low end): from the top-front edge down to the underside-front edge.
        MeshBuilder.AddQuad(side, tlf, blf, brf, trf, new Vector3(-l, -r, 0) / ls);
        // Back end cap (high end).
        MeshBuilder.AddQuad(side, trb, brb, blb, tlb, new Vector3(l, r, 0) / ls);

        // Left (−Z) and right (+Z) side faces (parallelograms).
        MeshBuilder.AddQuad(side, tlf, tlb, blb, blf, new Vector3(0, 0, -1));
        MeshBuilder.AddQuad(side, trf, brf, brb, trb, new Vector3(0, 0, 1));

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
