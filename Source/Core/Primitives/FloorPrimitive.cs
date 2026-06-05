using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// An axis-aligned rectangular floor slab, centred on the instance origin.
/// Top face sits at local y = 0; the slab extends down by <c>thickness</c>.
/// Surfaces: 0 = Top, 1 = Bottom, 2 = Edge.
/// </summary>
public sealed class FloorPrimitive : IPrimitive
{
    public string TypeId => "floor";
    public string DisplayName => "Floor";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("width",     "Width",     ParamType.Float, 4.0f, 0.1f, 1000f),
        new ParamSpec("depth",     "Depth",     ParamType.Float, 4.0f, 0.1f, 1000f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 100f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Top", "Bottom", "Edge" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float w = GetF(data, "width", 4.0f);
        float d = GetF(data, "depth", 4.0f);
        float t = GetF(data, "thickness", 0.2f);
        float hw = w * 0.5f, hd = d * 0.5f;

        // Top ring (y = 0): p0..p3, bottom ring (y = -t): p4..p7
        var p = new[]
        {
            new Vector3(-hw, 0, -hd), // 0
            new Vector3(-hw, 0,  hd), // 1
            new Vector3( hw, 0,  hd), // 2
            new Vector3( hw, 0, -hd), // 3
            new Vector3(-hw, -t, -hd), // 4
            new Vector3(-hw, -t,  hd), // 5
            new Vector3( hw, -t,  hd), // 6
            new Vector3( hw, -t, -hd), // 7
        };

        var mesh = new ArrayMesh();

        // Surface 0: Top (+Y)
        SurfaceTool top = Begin();
        MeshBuilder.AddQuad(top, p[0], p[1], p[2], p[3], Vector3.Up);
        Commit(top, mesh);

        // Surface 1: Bottom (-Y)
        SurfaceTool bottom = Begin();
        MeshBuilder.AddQuad(bottom, p[4], p[7], p[6], p[5], Vector3.Down);
        Commit(bottom, mesh);

        // Surface 2: Edge (4 sides), windings verified so each front face points outward.
        SurfaceTool edge = Begin();
        MeshBuilder.AddQuad(edge, p[0], p[3], p[7], p[4], new Vector3(0, 0, -1)); // -Z front
        MeshBuilder.AddQuad(edge, p[2], p[1], p[5], p[6], new Vector3(0, 0,  1)); // +Z back
        MeshBuilder.AddQuad(edge, p[1], p[0], p[4], p[5], new Vector3(-1, 0, 0)); // -X left
        MeshBuilder.AddQuad(edge, p[3], p[2], p[6], p[7], new Vector3( 1, 0, 0)); // +X right
        Commit(edge, mesh);

        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
    {
        // Trimesh from the same geometry — generic and exact for any future primitive.
        return new Shape3D[] { BuildMesh(data, ctx).CreateTrimeshShape() };
    }

    private static SurfaceTool Begin()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        return st;
    }

    private static void Commit(SurfaceTool st, ArrayMesh mesh)
    {
        st.GenerateTangents(); // normals are set explicitly per vertex; tangents for normal maps
        st.Commit(mesh);
    }

    private static float GetF(PrimitiveInstanceData d, string key, float def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsSingle() : def;
}
