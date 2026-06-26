using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Geometry;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// A reusable overlay of small marker cubes at a list of world points — the first point larger and orange
/// (the ring's start / where to click to close), the rest small and pale. Used by the polygon-floor draw
/// tool and the cut-hole tool so a ring's corners are visible from the first click (before the slab can be
/// filled) and the start is distinct. Manages its own node on a parent (vertex-colour material, drawn over
/// the geometry so corners are never hidden).
/// </summary>
public sealed class CornerMarkers
{
    private static readonly Color StartColor = new(1.0f, 0.55f, 0.1f);   // orange — where the ring begins / closes
    private static readonly Color PointColor = new(0.85f, 0.95f, 1.0f);  // pale — the other placed corners

    private MeshInstance3D _node;

    public void Show(Node parent, IReadOnlyList<Vector3> worldPoints)
    {
        if (worldPoints.Count == 0) { Hide(); return; }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < worldPoints.Count; i++)
        {
            bool start = i == 0;
            AddCube(st, worldPoints[i], start ? 0.18f : 0.09f, start ? StartColor : PointColor);
        }
        var mesh = new ArrayMesh();
        st.Commit(mesh);

        if (_node == null)
        {
            _node = new MeshInstance3D
            {
                Name = "CornerMarkers",
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                    NoDepthTest = true, // draw markers over the fill / grid so corners are never hidden
                },
            };
            parent.AddChild(_node);
        }
        _node.Mesh = mesh;
        _node.Visible = true;
    }

    public void Hide()
    {
        if (_node != null) _node.Visible = false;
    }

    /// <summary>A small axis-aligned cube of half-extent <paramref name="r"/> at <paramref name="c"/>, tinted
    /// <paramref name="col"/> (the marker material reads vertex colour as albedo). Winding from MeshBuilder.Box.</summary>
    private static void AddCube(SurfaceTool st, Vector3 c, float r, Color col)
    {
        st.SetColor(col);
        Vector3 a = c + new Vector3(-r, -r, r), b = c + new Vector3(r, -r, r),
                d = c + new Vector3(r, r, r), e = c + new Vector3(-r, r, r),
                f = c + new Vector3(-r, -r, -r), g = c + new Vector3(r, -r, -r),
                h = c + new Vector3(r, r, -r), k = c + new Vector3(-r, r, -r);
        MeshBuilder.AddQuad(st, a, b, d, e, new Vector3(0, 0, 1));   // +Z
        MeshBuilder.AddQuad(st, g, f, k, h, new Vector3(0, 0, -1));  // -Z
        MeshBuilder.AddQuad(st, e, d, h, k, Vector3.Up);            // +Y
        MeshBuilder.AddQuad(st, f, g, b, a, Vector3.Down);          // -Y
        MeshBuilder.AddQuad(st, b, g, h, d, new Vector3(1, 0, 0));   // +X
        MeshBuilder.AddQuad(st, f, a, e, k, new Vector3(-1, 0, 0));  // -X
    }
}
