using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Geometry;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a polygon floor: each click drops an outline corner on the grid; the slab is rubber-banded as a
/// closed polygon through them. Finish by clicking the FIRST corner again (≥3 placed) to close the ring;
/// Esc / right-click cancels. The thickness takes a default and is tuned afterwards in the inspector.
///
/// Each placed corner shows a small marker cube (visible from the very first click, before the slab can
/// be filled at ≥3 points), and the START corner is drawn larger and in a distinct colour so it's clear
/// where the ring began and where to click to close it. Markers live on a tool-owned node (vertex
/// colours, drawn over the geometry) separate from the green fill preview.
///
/// Like the path tool, the outline lays on the active draw plane (X/Z only) — a polygon floor is planar.
/// </summary>
public sealed class PolygonFloorDrawTool : DrawToolBase
{
    public override string Name => "Polygon Floor";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private static readonly Color StartColor = new(1.0f, 0.55f, 0.1f);   // orange — where the ring begins / closes
    private static readonly Color PointColor = new(0.85f, 0.95f, 1.0f);  // pale — the other placed corners

    private readonly List<Vector3> _points = new();
    private MeshInstance3D _markers;

    protected override void ResetState()
    {
        _points.Clear();
        HideMarkers();
    }

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        var p = new Vector3(corner.Value.X, 0, corner.Value.Z);

        // Click the FIRST corner again (≥3 placed) to close the polygon and commit.
        if (_points.Count >= 3 && p.DistanceTo(_points[0]) < 0.001f) { Commit(); return; }

        if (_points.Count > 0 && p.DistanceTo(_points[^1]) < 0.001f) return; // ignore zero-length segment
        _points.Add(p);
    }

    public override void UpdatePreview()
    {
        ShowMarkers();

        if (_points.Count == 0) { HidePreview(); return; }

        var pts = new List<Vector3>(_points);
        if (Ctx.Cursor.HoveredCorner is Vector3 hov)
        {
            var h = new Vector3(hov.X, 0, hov.Z);
            // Don't append the hover when it sits on the first (closing) or last point.
            bool onFirst = _points.Count >= 3 && h.DistanceTo(_points[0]) < 0.001f;
            if (!onFirst && h.DistanceTo(pts[^1]) > 0.001f) pts.Add(h);
        }
        if (pts.Count < 3) { HidePreview(); return; }

        PrimitiveInstanceData inst = Build(pts);
        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("polygon_floor").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private void Commit()
    {
        if (_points.Count >= 3) Ctx.AddInstance(Build(_points));
        ResetState();
        HidePreview();
    }

    private PrimitiveInstanceData Build(List<Vector3> pts)
    {
        var origin = new Vector3(pts[0].X, 0, pts[0].Z);
        var local = new Array<Vector3>();
        foreach (Vector3 p in pts) local.Add(new Vector3(p.X - origin.X, 0, p.Z - origin.Z));

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "polygon_floor",
            LocalTransform = new Transform3D(Basis.Identity, origin),
            Parameters = new Dictionary
            {
                { "points", local },
                { "thickness", 0.2 },
            },
        };
    }

    // --- Corner markers (separate node so the start can be its own colour). ---

    private void ShowMarkers()
    {
        if (_points.Count == 0) { HideMarkers(); return; }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < _points.Count; i++)
        {
            bool start = i == 0;
            AddCube(st, _points[i] + Ctx.ElevationOffset, start ? 0.18f : 0.09f, start ? StartColor : PointColor);
        }
        var mesh = new ArrayMesh();
        st.Commit(mesh);

        if (_markers == null)
        {
            _markers = new MeshInstance3D
            {
                Name = "PolygonDrawMarkers",
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                    NoDepthTest = true, // draw the markers over the fill / grid so corners are never hidden
                },
            };
            Ctx.PreviewLayer.AddChild(_markers);
        }
        _markers.Mesh = mesh;
        _markers.Visible = true;
    }

    private void HideMarkers()
    {
        if (_markers != null) _markers.Visible = false;
    }

    /// <summary>A small axis-aligned cube of half-extent <paramref name="r"/> at <paramref name="c"/>, with
    /// every vertex tinted <paramref name="col"/> (the marker material reads vertex colour as albedo).</summary>
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
