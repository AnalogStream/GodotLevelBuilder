using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A straight wall segment with optional door/window openings. Built by box-decomposition
/// (docs/PRIMITIVES.md): the wall along its length splits into solid sub-boxes — full-height
/// segments between openings, plus a sill block below and header block above each opening —
/// and the four inner faces of every opening (jambs, sill top, header underside) are emitted
/// as reveal quads. No CSG, no polygon-with-hole; holes show in the mesh and the trimesh
/// collision alike.
///
/// Local space: centred on origin, running along X (length), thickness along Z, rising y=0..H.
/// Surfaces: 0 Front (+Z), 1 Back (−Z), 2 Top (+Y), 3 Ends (bottom + X caps), 4 Reveal.
/// </summary>
public sealed class WallPrimitive : IPrimitive
{
    private const float Eps = 1e-4f;

    public string TypeId => "wall";
    public string DisplayName => "Wall";
    public string Category => "Structure";

    public IReadOnlyList<ParamSpec> Parameters { get; } = new[]
    {
        new ParamSpec("length",    "Length",    ParamType.Float, 1.0f, 0.01f, 1000f),
        new ParamSpec("height",    "Height",    ParamType.Float, 3.0f, 0.05f, 100f),
        new ParamSpec("thickness", "Thickness", ParamType.Float, 0.2f, 0.01f, 10f),
    };

    public IReadOnlyList<string> MaterialSlots { get; } = new[] { "Front", "Back", "Top", "Ends", "Reveal" };

    public ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx)
    {
        float l = GetF(data, "length", 1.0f);
        float h = GetF(data, "height", ctx.StoreyHeight);
        float t = GetF(data, "thickness", 0.2f);
        float zt = t * 0.5f, zb = -t * 0.5f;

        SurfaceTool front = Begin(), back = Begin(), top = Begin(), ends = Begin(), reveal = Begin();

        float X(float u) => u - l * 0.5f;

        void FrontBack(float ua, float ub, float ya, float yb)
        {
            float xa = X(ua), xb = X(ub);
            MeshBuilder.AddQuad(front, new(xa, ya, zt), new(xb, ya, zt), new(xb, yb, zt), new(xa, yb, zt), new Vector3(0, 0, 1));
            MeshBuilder.AddQuad(back, new(xb, ya, zb), new(xa, ya, zb), new(xa, yb, zb), new(xb, yb, zb), new Vector3(0, 0, -1));
        }
        void TopStrip(float ua, float ub)
        {
            float xa = X(ua), xb = X(ub);
            MeshBuilder.AddQuad(top, new(xa, h, zt), new(xb, h, zt), new(xb, h, zb), new(xa, h, zb), Vector3.Up);
        }
        void BottomStrip(float ua, float ub)
        {
            float xa = X(ua), xb = X(ub);
            MeshBuilder.AddQuad(ends, new(xa, 0, zb), new(xb, 0, zb), new(xb, 0, zt), new(xa, 0, zt), Vector3.Down);
        }
        void Reveals(OpeningBox o)
        {
            float x0 = X(o.Offset), x1 = X(o.Offset + o.Width);
            // left jamb (+X) and right jamb (−X) span the void height
            MeshBuilder.AddQuad(reveal, new(x0, o.Sill, zt), new(x0, o.Sill, zb), new(x0, o.Top, zb), new(x0, o.Top, zt), new Vector3(1, 0, 0));
            MeshBuilder.AddQuad(reveal, new(x1, o.Sill, zb), new(x1, o.Sill, zt), new(x1, o.Top, zt), new(x1, o.Top, zb), new Vector3(-1, 0, 0));
            if (o.Sill > Eps) // sill top faces up into the opening
                MeshBuilder.AddQuad(reveal, new(x0, o.Sill, zt), new(x1, o.Sill, zt), new(x1, o.Sill, zb), new(x0, o.Sill, zb), Vector3.Up);
            if (o.Top < h - Eps) // header underside faces down
                MeshBuilder.AddQuad(reveal, new(x0, o.Top, zb), new(x1, o.Top, zb), new(x1, o.Top, zt), new(x0, o.Top, zt), Vector3.Down);
        }

        List<OpeningBox> openings = CollectValid(data.Openings, l, h);

        float cursor = 0f;
        foreach (OpeningBox o in openings)
        {
            if (o.Offset > cursor + Eps) { FrontBack(cursor, o.Offset, 0, h); TopStrip(cursor, o.Offset); BottomStrip(cursor, o.Offset); }
            if (o.Sill > Eps) { FrontBack(o.Offset, o.Offset + o.Width, 0, o.Sill); BottomStrip(o.Offset, o.Offset + o.Width); }
            if (o.Top < h - Eps) { FrontBack(o.Offset, o.Offset + o.Width, o.Top, h); TopStrip(o.Offset, o.Offset + o.Width); }
            Reveals(o);
            cursor = o.Offset + o.Width;
        }
        if (cursor < l - Eps) { FrontBack(cursor, l, 0, h); TopStrip(cursor, l); BottomStrip(cursor, l); }

        // End caps at u = 0 (−X) and u = L (+X)
        float xMin = X(0f), xMax = X(l);
        MeshBuilder.AddQuad(ends, new(xMin, 0, zb), new(xMin, 0, zt), new(xMin, h, zt), new(xMin, h, zb), new Vector3(-1, 0, 0));
        MeshBuilder.AddQuad(ends, new(xMax, 0, zt), new(xMax, 0, zb), new(xMax, h, zb), new(xMax, h, zt), new Vector3(1, 0, 0));

        var mesh = new ArrayMesh();
        Commit(front, mesh);
        Commit(back, mesh);
        Commit(top, mesh);
        Commit(ends, mesh);
        if (openings.Count > 0) Commit(reveal, mesh);
        return mesh;
    }

    public Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx)
        => new Shape3D[] { BuildMesh(data, ctx).CreateTrimeshShape() };

    /// <summary>Clamp/sort openings to interior, non-overlapping, non-degenerate boxes.</summary>
    private static List<OpeningBox> CollectValid(Godot.Collections.Array<OpeningData> raw, float l, float h)
    {
        var boxes = new List<OpeningBox>();
        foreach (OpeningData o in raw)
        {
            float width = o.Width;
            float offset = o.Offset;
            if (width <= Eps || offset <= Eps || offset + width >= l - Eps) continue; // must be interior
            float sill = Mathf.Max(0f, o.SillHeight);
            float topY = Mathf.Min(sill + o.Height, h);
            if (topY <= sill + Eps) continue;
            boxes.Add(new OpeningBox { Offset = offset, Width = width, Sill = sill, Top = topY });
        }
        boxes.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        // Drop any that overlap an earlier one.
        var result = new List<OpeningBox>();
        float cursor = 0f;
        foreach (OpeningBox o in boxes)
        {
            if (o.Offset < cursor - Eps) continue;
            result.Add(o);
            cursor = o.Offset + o.Width;
        }
        return result;
    }

    private struct OpeningBox
    {
        public float Offset, Width, Sill, Top;
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
}
