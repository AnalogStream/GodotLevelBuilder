using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A spherical/elliptical cap that is either a DOME (convex, <c>convex</c>=true) you roll over or a BOWL
/// (concave) you roll inside — both classic Super Monkey Ball shapes. The footprint is a circle of
/// <c>radius</c>; <c>height</c> is the cap's vertical extent (height=radius → a true hemisphere, otherwise
/// a squashed/stretched ellipsoidal cap). Tessellated into <c>rings</c> bands up the profile × <c>sides</c>
/// facets around.
///
/// Dome: apex at y=<c>height</c>, base rim at y=0; solid (cap surface + flat underside). Bowl: rim at
/// y=<c>height</c>, lowest point at y=0; a solid cup (concave dish + outer cylinder wall + flat underside).
/// The dish meets the wall at the rim and touches the underside at the centre, so the cup is watertight.
///
/// Surfaces: 0 Surface (the cap/dish you roll on), 1 Bottom (underside disc), 2 Side (the bowl's outer
/// wall — emitted only for a bowl, so it stays the trailing slot and the index→slot mapping is stable).
/// Winding is delegated to <see cref="MeshBuilder.AddQuadFacing"/>/<see cref="MeshBuilder.AddTriFacing"/>.
/// </summary>
public sealed class DomePrimitive : IPrimitive
{
    public string TypeId => "dome";
    public string DisplayName => "Dome / Bowl";
    public string Category => "Curves";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("radius", "Radius",        ParamType.Float, 2.0f, 0.1f,  100f),
        new ParamSpec("height", "Height",        ParamType.Float, 2.0f, 0.05f, 100f),
        new ParamSpec("convex", "Convex (Dome)", ParamType.Bool,  true),
        new ParamSpec("rings",  "Rings",         ParamType.Int,   8,    2f,    64f),
        new ParamSpec("sides",  "Sides",         ParamType.Int,   16,   3f,    128f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Surface", "Bottom", "Side" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float R = GetF(data, "radius", 2f);
        float H = GetF(data, "height", 2f);
        bool convex = GetB(data, "convex", true);
        int rings = Mathf.Max(2, GetI(data, "rings", 8));
        int sides = Mathf.Max(3, GetI(data, "sides", 16));
        var up = Vector3.Up;

        // Ring profile: ψ from 0 (base rim) to π/2 (apex/centre). ρ = R·cosψ. Dome climbs (y=H·sinψ),
        // bowl descends from the rim (y=H·(1−sinψ)) so the concave side opens upward.
        var rho = new float[rings + 1];
        var yj = new float[rings + 1];
        for (int j = 0; j <= rings; j++)
        {
            float psi = (Mathf.Pi * 0.5f) * j / rings;
            rho[j] = R * Mathf.Cos(psi);
            yj[j] = convex ? H * Mathf.Sin(psi) : H * (1f - Mathf.Sin(psi));
        }

        // Angles around + the per-facet mid radial (for the face's "should look this way" reference).
        var cosA = new float[sides + 1];
        var sinA = new float[sides + 1];
        for (int k = 0; k <= sides; k++) { float a = Mathf.Tau * k / sides; cosA[k] = Mathf.Cos(a); sinA[k] = Mathf.Sin(a); }

        Vector3 S(int j, int k) => new(rho[j] * cosA[k], yj[j], rho[j] * sinA[k]);
        Vector3 MidRadial(int k) { float am = Mathf.Tau * (k + 0.5f) / sides; return new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am)); }

        SurfaceTool surface = Begin(), bottom = Begin(), side = Begin();
        var apex = new Vector3(0, yj[rings], 0);   // ρ=0 singularity (dome apex / bowl centre)
        var baseC = new Vector3(0, 0, 0);

        // --- Cap surface: convex faces outward+up, concave faces inward+up. ---
        for (int k = 0; k < sides; k++)
        {
            int m = k + 1;
            Vector3 refN = (convex ? MidRadial(k) : -MidRadial(k)) + up;
            for (int j = 0; j < rings; j++)
            {
                if (j == rings - 1) // top band collapses to the apex/centre point → triangle fan
                    MeshBuilder.AddTriFacing(surface, S(j, k), S(j, m), apex, refN);
                else
                    MeshBuilder.AddQuadFacing(surface, S(j, k), S(j, m), S(j + 1, m), S(j + 1, k), refN);
            }
        }

        // --- Underside disc at y=0 (faces down). ---
        for (int k = 0; k < sides; k++)
        {
            int m = k + 1;
            var b0 = new Vector3(R * cosA[k], 0, R * sinA[k]);
            var b1 = new Vector3(R * cosA[m], 0, R * sinA[m]);
            MeshBuilder.AddTriFacing(bottom, baseC, b0, b1, Vector3.Down);
        }

        bool bowl = !convex;
        if (bowl)
        {
            // Outer cylinder wall (faces outward), R from y=0 up to the rim at y=H = the dish rim.
            for (int k = 0; k < sides; k++)
            {
                int m = k + 1;
                var lo0 = new Vector3(R * cosA[k], 0, R * sinA[k]);
                var lo1 = new Vector3(R * cosA[m], 0, R * sinA[m]);
                var hi0 = new Vector3(R * cosA[k], H, R * sinA[k]);
                var hi1 = new Vector3(R * cosA[m], H, R * sinA[m]);
                MeshBuilder.AddQuadFacing(side, lo0, lo1, hi1, hi0, MidRadial(k));
            }
        }

        var mesh = new ArrayMesh();
        Commit(surface, mesh);   // slot 0
        Commit(bottom, mesh);    // slot 1
        if (bowl) Commit(side, mesh); // slot 2 (trailing + conditional → stable mapping)
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

    private static bool GetB(PrimitiveInstanceData d, string key, bool def)
        => d.Parameters.ContainsKey(key) ? d.Parameters[key].AsBool() : def;
}
