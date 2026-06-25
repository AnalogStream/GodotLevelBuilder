using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a path-swept primitive: each click drops a control point on the grid; the path is rubber-banded
/// through them and a cross-section is swept along the smoothed curve. Finish by clicking the last point
/// again (open path), or click the FIRST point (≥3 placed) to finish as a CLOSED loop; Esc / right-click
/// cancels the whole path. The cross-section profile and its dimensions take defaults and are tuned
/// afterwards in the inspector (including a "Closed Loop" toggle to switch an existing path either way).
///
/// Slice 1 lays the path on the storey floor plane (X/Z only) — per-point height and bank, which steep /
/// looping tracks need, come from the point-edit gizmo (a later slice), not the floor click.
/// </summary>
public sealed class PathSweepDrawTool : DrawToolBase
{
    public override string Name => "Path Sweep";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private readonly List<Vector3> _points = new();

    protected override void ResetState() => _points.Clear();

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        var p = new Vector3(corner.Value.X, 0, corner.Value.Z);

        // Click the FIRST point again (≥3 placed) to finish as a CLOSED loop.
        if (_points.Count >= 3 && p.DistanceTo(_points[0]) < 0.001f) { Commit(closed: true); return; }

        bool atLast = _points.Count > 0 && p.DistanceTo(_points[^1]) < 0.001f;
        if (atLast)
        {
            if (_points.Count >= 2) Commit(closed: false); // click the last point again to finish (open)
            return;                                         // otherwise ignore (zero-length segment)
        }
        _points.Add(p);
    }

    public override void UpdatePreview()
    {
        if (_points.Count == 0) { HidePreview(); return; }

        var pts = new List<Vector3>(_points);
        bool closing = false;
        if (Ctx.Cursor.HoveredCorner is Vector3 hov)
        {
            var h = new Vector3(hov.X, 0, hov.Z);
            if (_points.Count >= 3 && h.DistanceTo(_points[0]) < 0.001f) closing = true; // hovering the first point
            else if (h.DistanceTo(pts[^1]) > 0.001f) pts.Add(h);
        }
        if (pts.Count < 2) { HidePreview(); return; }

        PrimitiveInstanceData inst = Build(pts, closing);
        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("path_sweep").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private void Commit(bool closed)
    {
        if (_points.Count >= 2) Ctx.AddInstance(Build(_points, closed));
        ResetState();
        HidePreview();
    }

    private PrimitiveInstanceData Build(List<Vector3> pts, bool closed)
    {
        var origin = new Vector3(pts[0].X, 0, pts[0].Z);
        var local = new Array<Vector3>();
        foreach (Vector3 p in pts) local.Add(new Vector3(p.X - origin.X, 0, p.Z - origin.Z));

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "path_sweep",
            LocalTransform = new Transform3D(Basis.Identity, origin),
            Parameters = new Dictionary
            {
                { "points", local },
                { "profile", 0 },
                { "width", (double)(Ctx.Document.Grid.CellSize * 2f) },
                { "thickness", 0.2 },
                { "bank", 0.0 },
                { "wallHeight", 3.0 },
                { "radius", 1.5 },
                { "arc", 180.0 },
                { "sides", 12 },
                { "segments", 8 },
                { "closed", closed },
            },
        };
    }
}
