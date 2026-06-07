using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// An upright cylinder — a pillar/post or a round platform. <c>radius</c> across, <c>height</c> tall,
/// standing on the local origin (base at y=0, top at y=<c>height</c>), tessellated into <c>sides</c>
/// facets. The walkable top and the underside are flat caps.
///
/// Surfaces: 0 Side (the curved wall), 1 Top (+Y cap), 2 Bottom (−Y cap). The side walls carry a
/// continuous U around the circumference (V up the height); the caps are top-down planar.
/// </summary>
public sealed class CylinderPrimitive : IPrimitive
{
    public string TypeId => "cylinder";
    public string DisplayName => "Cylinder";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("radius", "Radius", ParamType.Float, 1.0f, 0.1f,  100f),
        new ParamSpec("height", "Height", ParamType.Float, 3.0f, 0.05f, 100f),
        new ParamSpec("sides",  "Sides",  ParamType.Int,   16,   3f,    128f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Side", "Top", "Bottom" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float r = GetF(data, "radius", 1f);
        float h = GetF(data, "height", 3f);
        int sides = Mathf.Max(3, GetI(data, "sides", 16));

        // Ring of base/top positions + the per-station outward radial (for the wall normal and UV).
        var baseP = new Vector3[sides + 1];
        var topP = new Vector3[sides + 1];
        var radial = new Vector3[sides + 1];
        var circU = new float[sides + 1];
        for (int k = 0; k <= sides; k++)
        {
            float a = Mathf.Tau * k / sides;
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            radial[k] = new Vector3(cx, 0, cz);
            baseP[k] = new Vector3(r * cx, 0, r * cz);
            topP[k] = new Vector3(r * cx, h, r * cz);
            circU[k] = r * a;
        }

        SurfaceTool side = Begin(), top = Begin(), bottom = Begin();
        var topC = new Vector3(0, h, 0);
        var baseC = new Vector3(0, 0, 0);

        for (int k = 0; k < sides; k++)
        {
            int m = k + 1;
            Vector3 nOut = ((radial[k] + radial[m]) * 0.5f).Normalized();

            // Curved wall (faces outward): U around the circumference, V up the wall (bottom V=h, top V=0).
            MeshBuilder.AddQuad(side, baseP[k], topP[k], topP[m], baseP[m], nOut,
                new Vector2(circU[k], h), new Vector2(circU[k], 0),
                new Vector2(circU[m], 0), new Vector2(circU[m], h));

            // Caps: fan from the centre, oriented by reference (up / down).
            MeshBuilder.AddTriFacing(top, topC, topP[k], topP[m], Vector3.Up);
            MeshBuilder.AddTriFacing(bottom, baseC, baseP[k], baseP[m], Vector3.Down);
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

    private static int GetI(PrimitiveInstanceData d, string key, int def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsInt32() : def;
}
