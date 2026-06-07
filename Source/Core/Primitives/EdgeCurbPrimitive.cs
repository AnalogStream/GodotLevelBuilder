using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A rectangular curb / rim — the low lip that runs around the edge of a Super Monkey Ball platform to
/// keep the ball from rolling straight off. A picture-frame loop: the OUTER rectangle is
/// <c>width</c>×<c>depth</c> (centred on the origin, base at local y=0), the wall band is
/// <c>thickness</c> wide and <c>railHeight</c> tall, rising inward to an inner rectangle inset by the
/// thickness. The four corners miter at 45°. Built solid (outer + inner walls, mitred top band, and an
/// underside band) so it reads correctly from every side and bakes a closed trimesh.
///
/// Surfaces: 0 Side (outer + inner vertical faces), 1 Top (the band you'd walk on), 2 Bottom (underside).
/// Winding is delegated to <see cref="MeshBuilder.AddQuadFacing"/> — each face just states which way it
/// should look (outward for walls, up for the top, down for the bottom).
/// </summary>
public sealed class EdgeCurbPrimitive : IPrimitive
{
    public string TypeId => "edge_curb";
    public string DisplayName => "Edge Curb";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("width",      "Width",       ParamType.Float, 4.0f,  0.2f,  1000f),
        new ParamSpec("depth",      "Depth",       ParamType.Float, 4.0f,  0.2f,  1000f),
        new ParamSpec("railHeight", "Rail Height", ParamType.Float, 0.3f,  0.02f, 50f),
        new ParamSpec("thickness",  "Thickness",   ParamType.Float, 0.15f, 0.02f, 50f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Side", "Top", "Bottom" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float width = GetF(data, "width", 4f);
        float depth = GetF(data, "depth", 4f);
        float h = GetF(data, "railHeight", 0.3f);
        float t = GetF(data, "thickness", 0.15f);
        // Clamp so the inner rectangle never collapses or inverts (keeps a sliver of floor in the middle).
        t = Mathf.Min(t, 0.49f * Mathf.Min(width, depth));

        float hw = width * 0.5f, hd = depth * 0.5f;
        var up = Vector3.Up;

        // Outer ring (CCW from above) and the matching inner ring inset by the thickness.
        var outer = new[]
        {
            new Vector2(-hw, -hd), new Vector2(-hw, hd), new Vector2(hw, hd), new Vector2(hw, -hd),
        };
        var inner = new[]
        {
            new Vector2(-hw + t, -hd + t), new Vector2(-hw + t, hd - t),
            new Vector2(hw - t, hd - t), new Vector2(hw - t, -hd + t),
        };
        // Outward horizontal normal for each of the four sides, in ring order (edge k → k+1).
        var faceNormal = new[]
        {
            new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 0, -1),
        };

        SurfaceTool side = Begin(), top = Begin(), bottom = Begin();

        Vector3 Lo(Vector2 p) => new(p.X, 0, p.Y);   // base (y=0)
        Vector3 Hi(Vector2 p) => new(p.X, h, p.Y);   // top  (y=h)

        for (int k = 0; k < 4; k++)
        {
            int m = (k + 1) % 4;
            Vector3 nOut = faceNormal[k];

            // Outer wall (faces out) and inner wall (faces in) of this side.
            MeshBuilder.AddQuadFacing(side, Lo(outer[k]), Lo(outer[m]), Hi(outer[m]), Hi(outer[k]), nOut);
            MeshBuilder.AddQuadFacing(side, Lo(inner[k]), Lo(inner[m]), Hi(inner[m]), Hi(inner[k]), -nOut);

            // Top band (faces up) and underside band (faces down): the mitred strip outer↔inner.
            MeshBuilder.AddQuadFacing(top, Hi(outer[k]), Hi(outer[m]), Hi(inner[m]), Hi(inner[k]), up);
            MeshBuilder.AddQuadFacing(bottom, Lo(outer[k]), Lo(outer[m]), Lo(inner[m]), Lo(inner[k]), Vector3.Down);
        }

        var mesh = new ArrayMesh();
        Commit(side, mesh);
        Commit(top, mesh);
        Commit(bottom, mesh);
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
