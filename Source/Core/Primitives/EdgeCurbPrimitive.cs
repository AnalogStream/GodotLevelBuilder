using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A rectangular curb / rim — the lip that runs around the edge of a Super Monkey Ball platform to keep
/// the ball from rolling straight off. The outline is always the <c>width</c>×<c>depth</c> rectangle
/// (centred on the origin, base at local y=0); the <c>style</c> dropdown picks WHAT runs around it, using
/// the same three rim styles as the polygon-floor / path-sweep rails:
///
///   0 Rail          — the original solid picture-frame curb. The wall band is <c>thickness</c> wide and
///                     <c>railHeight</c> tall, rising inward to an inset inner rectangle, corners mitred at
///                     45°. Built closed (outer + inner walls, mitred top band, underside band) so it reads
///                     from every side and bakes a closed trimesh — surfaces 0 Side, 1 Top, 2 Bottom.
///   1 Elevated Rail — posts (every ~2.5 m) + a top beam: a fence around the rectangle.
///   2 Bank          — an angled wedge that funnels the ball back toward centre; <c>bankAngle</c> sets the
///                     slope (and its sign the lean), <c>railHeight</c> the rise.
///
/// Only the Rail style emits the full Side/Top/Bottom solid; Elevated Rail and Bank are delegated to the
/// shared <see cref="RailBuilder"/> (the exact generators the floor/path rails use), which emits a SINGLE
/// surface — so they map onto the leading "Side" slot, leaving Top/Bottom unused (the resolver/baker index
/// MaterialSlots by committed-surface index, so a present-prefix is fine). Enum is APPEND-ONLY for round-trip
/// stability; default 0 = Rail keeps every existing curb (which has no "style" key) looking exactly as before.
/// Winding is delegated to <see cref="MeshBuilder.AddQuadFacing"/> — each face just states which way it looks.
/// </summary>
public sealed class EdgeCurbPrimitive : IPrimitive
{
    public string TypeId => "edge_curb";
    public string DisplayName => "Edge Curb";
    public string Category => "Structure";

    // Style enum (the "style" param). APPEND-ONLY (index = stored value); default Rail keeps legacy curbs.
    private const int StyleRail = 0, StyleElevated = 1, StyleBank = 2;

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("width",      "Width",       ParamType.Float, 4.0f,  0.2f,  1000f),
        new ParamSpec("depth",      "Depth",       ParamType.Float, 4.0f,  0.2f,  1000f),
        // What runs around the rectangle — same three rim styles as the floor / path rails. Append-only.
        new ParamSpec("style",      "Style",       ParamType.Int,   0, 0f, 2f, new[] { "Rail", "Elevated Rail", "Bank" }),
        new ParamSpec("railHeight", "Rail Height", ParamType.Float, 0.3f,  0.02f, 50f),
        new ParamSpec("thickness",  "Thickness",   ParamType.Float, 0.15f, 0.02f, 50f),
        // Bank slope from horizontal (degrees): the wedge drops railHeight over a run of height/tan(|angle|);
        // SIGN leans it (positive = high lip OUTER edge sloping inward; negative = high lip INNER edge). Bank only.
        new ParamSpec("bankAngle",  "Bank Angle",  ParamType.Float, 45f, -85f, 85f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Side", "Top", "Bottom" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        int style = GetI(data, "style", StyleRail);
        if (style != StyleRail) return BuildRailStyle(data, style);

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

    /// <summary>The Elevated Rail / Bank styles: feed the width×depth rectangle to the shared
    /// <see cref="RailBuilder"/> (the exact generators the floor / path rails use). It emits ONE surface, so it
    /// commits as surface 0 → the "Side" slot (Top/Bottom stay unused). Unlike the Rail style this has no
    /// underside band — fine for a curb sitting on the ground, where collision is top-side anyway.</summary>
    private static ArrayMesh BuildRailStyle(PrimitiveInstanceData data, int style)
    {
        var mesh = new ArrayMesh();
        float width = GetF(data, "width", 4f);
        float depth = GetF(data, "depth", 4f);
        float h = GetF(data, "railHeight", 0.3f);
        float w = GetF(data, "thickness", 0.15f);
        float bankAngle = GetF(data, "bankAngle", 45f);

        float hw = width * 0.5f, hd = depth * 0.5f;
        // Outer ring (CCW from above), centred on the origin; centroid is the origin.
        var outer = new[]
        {
            new Vector2(-hw, -hd), new Vector2(-hw, hd), new Vector2(hw, hd), new Vector2(hw, -hd),
        };
        // RailBuilder style codes: 1 = curb, 2 = fence, 3 = bank.
        int railStyle = style == StyleElevated ? 2 : 3;

        SurfaceTool rail = Begin();
        if (RailBuilder.EmitRing(rail, outer, Vector2.Zero, railStyle, h, w, bankAngle, inward: true))
            Commit(rail, mesh);
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
