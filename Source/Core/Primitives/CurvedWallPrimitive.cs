using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A wall swept through a circular arc — the curved guard rail that follows a banked curve or half-pipe
/// rim so the ball can't roll off the turn. The centreline starts at the local origin heading along +X
/// and curves toward −Z for a positive <c>arc</c> (left) or +Z for a negative one (right), sweeping
/// |<c>arc</c>|° at centreline <c>radius</c>. The cross-section is an upright rectangle: <c>thickness</c>
/// across the path, <c>height</c> tall, base at y=0. Tessellated into <c>segments</c> rings.
///
/// Surfaces: 0 Front (outer face), 1 Back (inner face), 2 Top, 3 Ends (underside + the two end caps).
/// Winding is delegated to <see cref="MeshBuilder.AddQuadFacing"/>, so a right turn (the mirror of a
/// left) needs no special-casing — each face just declares the direction it should look.
/// </summary>
public sealed class CurvedWallPrimitive : IPrimitive
{
    public string TypeId => "curved_wall";
    public string DisplayName => "Curved Wall";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("radius",    "Radius",    ParamType.Float, 4.0f, 0.5f,   100f),
        new ParamSpec("arc",       "Arc (deg)", ParamType.Float, 90.0f, -270f, 270f),
        new ParamSpec("height",    "Height",    ParamType.Float, 1.0f, 0.05f,  50f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.02f,  10f),
        new ParamSpec("segments",  "Segments",  ParamType.Int,   16,   2f,     64f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Front", "Back", "Top", "Ends" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float radius = GetF(data, "radius", 4f);
        float arcDeg = GetF(data, "arc", 90f);
        float height = GetF(data, "height", 1f);
        float t = GetF(data, "thickness", 0.2f);
        int seg = Mathf.Max(2, GetI(data, "segments", 16));

        float dir = arcDeg >= 0 ? 1f : -1f;          // + curves toward −Z (left), − toward +Z (right)
        float arc = Mathf.Abs(Mathf.DegToRad(arcDeg));
        float hT = t * 0.5f;
        var up = Vector3.Up;

        var inB = new Vector3[seg + 1]; var ouB = new Vector3[seg + 1];
        var inT = new Vector3[seg + 1]; var ouT = new Vector3[seg + 1];
        var radialOut = new Vector3[seg + 1];

        for (int i = 0; i <= seg; i++)
        {
            float th = arc * i / seg;
            float s = Mathf.Sin(th), c = Mathf.Cos(th);
            var p = new Vector3(radius * s, 0, dir * (radius * c - radius));
            var ro = new Vector3(s, 0, dir * c);
            radialOut[i] = ro;
            inB[i] = p - hT * ro;
            ouB[i] = p + hT * ro;
            inT[i] = inB[i] + up * height;
            ouT[i] = ouB[i] + up * height;
        }

        SurfaceTool front = Begin(), back = Begin(), top = Begin(), ends = Begin();

        for (int i = 0; i < seg; i++)
        {
            int j = i + 1;
            Vector3 ro = ((radialOut[i] + radialOut[j]) * 0.5f).Normalized();

            MeshBuilder.AddQuadFacing(front, ouB[i], ouB[j], ouT[j], ouT[i], ro);   // outer face
            MeshBuilder.AddQuadFacing(back, inB[i], inB[j], inT[j], inT[i], -ro);   // inner face
            MeshBuilder.AddQuadFacing(top, inT[i], ouT[i], ouT[j], inT[j], up);     // top
            MeshBuilder.AddQuadFacing(ends, inB[i], ouB[i], ouB[j], inB[j], Vector3.Down); // underside
        }

        // End caps: the rectangular cross-section at each end, facing along ∓ the path tangent.
        var tan0 = new Vector3(1, 0, 0);
        var tanN = new Vector3(Mathf.Cos(arc), 0, -dir * Mathf.Sin(arc));
        MeshBuilder.AddQuadFacing(ends, inB[0], ouB[0], ouT[0], inT[0], -tan0);
        MeshBuilder.AddQuadFacing(ends, inB[seg], ouB[seg], ouT[seg], inT[seg], tanN);

        var mesh = new ArrayMesh();
        Commit(front, mesh);
        Commit(back, mesh);
        Commit(top, mesh);
        Commit(ends, mesh);
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
