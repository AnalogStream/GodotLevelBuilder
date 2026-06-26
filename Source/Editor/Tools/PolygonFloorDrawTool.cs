using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a polygon floor: each click drops an outline corner on the grid; the slab is rubber-banded as a
/// closed polygon through them. Finish by clicking the FIRST corner again (≥3 placed) to close the ring;
/// Esc / right-click cancels. The thickness takes a default and is tuned afterwards in the inspector.
///
/// Each placed corner shows a <see cref="CornerMarkers"/> cube (visible from the very first click, before
/// the slab can be filled at ≥3 points), the START corner distinct so it's clear where to click to close.
///
/// Like the path tool, the outline lays on the active draw plane (X/Z only) — a polygon floor is planar.
/// </summary>
public sealed class PolygonFloorDrawTool : DrawToolBase
{
    public override string Name => "Polygon Floor";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private readonly List<Vector3> _points = new();
    private readonly CornerMarkers _markers = new();

    protected override void ResetState()
    {
        _points.Clear();
        _markers.Hide();
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
        var markerPts = new List<Vector3>(_points.Count);
        foreach (Vector3 p in _points) markerPts.Add(p + Ctx.ElevationOffset);
        _markers.Show(Ctx.PreviewLayer, markerPts);

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
}
