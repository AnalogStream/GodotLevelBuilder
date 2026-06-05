using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A straight wall segment with optional door/window openings. Built by box-decomposition
/// (docs/PRIMITIVES.md): the wall's front/back faces are swept in two passes — split into vertical
/// strips at every opening edge, then within each strip the solid y-bands (between the openings
/// that cover it) are emitted as quads. This handles openings side by side AND stacked vertically
/// (a window above a door, two windows…). The four inner faces of every opening (jambs, sill top,
/// header underside) are emitted as reveal quads. No CSG, no polygon-with-hole; holes show in the
/// mesh and the trimesh collision alike.
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

        // Sweep the front/back faces in vertical strips bounded by every opening edge. Within each
        // strip, the openings that cover it stack up the height; emit the solid bands in the gaps.
        var bounds = new SortedSet<float> { 0f, l };
        foreach (OpeningBox o in openings) { bounds.Add(o.Offset); bounds.Add(o.Offset + o.Width); }
        var us = new List<float>(bounds);

        for (int i = 0; i + 1 < us.Count; i++)
        {
            float ua = us[i], ub = us[i + 1];
            if (ub - ua < Eps) continue;
            float mid = (ua + ub) * 0.5f;

            List<OpeningBox> inStrip = openings.FindAll(o => o.Offset <= mid && mid <= o.Offset + o.Width);
            inStrip.Sort((a, b) => a.Sill.CompareTo(b.Sill));

            float y = 0f;
            foreach (OpeningBox o in inStrip)
            {
                if (o.Sill > y + Eps) FrontBack(ua, ub, y, o.Sill);
                y = Mathf.Max(y, o.Top);
            }
            if (y < h - Eps) FrontBack(ua, ub, y, h);

            if (inStrip.Count == 0 || inStrip[0].Sill > Eps) BottomStrip(ua, ub); // floor face, unless an opening reaches it
            if (y < h - Eps) TopStrip(ua, ub);                                    // ceiling face, unless an opening reaches it
        }

        foreach (OpeningBox o in openings) Reveals(o);

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

    /// <summary>
    /// Interior, non-degenerate opening boxes, dropping any that overlap an earlier one in 2D
    /// (so vertically-stacked openings are kept — only true rectangle overlaps are rejected).
    /// </summary>
    private static List<OpeningBox> CollectValid(Godot.Collections.Array<OpeningData> raw, float l, float h)
    {
        var result = new List<OpeningBox>();
        foreach (OpeningData o in raw)
        {
            float width = o.Width;
            float offset = o.Offset;
            if (width <= Eps || offset <= Eps || offset + width >= l - Eps) continue; // must be interior
            float sill = Mathf.Max(0f, o.SillHeight);
            float topY = Mathf.Min(sill + o.Height, h);
            if (topY <= sill + Eps) continue;

            var box = new OpeningBox { Offset = offset, Width = width, Sill = sill, Top = topY };
            if (!OverlapsAny(box, result)) result.Add(box);
        }
        return result;
    }

    private static bool OverlapsAny(OpeningBox b, List<OpeningBox> existing)
    {
        foreach (OpeningBox e in existing)
            if (b.Offset < e.Offset + e.Width && b.Offset + b.Width > e.Offset &&
                b.Sill < e.Top && b.Top > e.Sill) return true;
        return false;
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
