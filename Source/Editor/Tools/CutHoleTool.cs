using System.Collections.Generic;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Cuts a hole into the SELECTED polygon floor: each click drops a hole-ring corner inside the slab; the
/// holed slab is rubber-banded live (the hole appears once the ring is valid). Finish by clicking the
/// FIRST corner again (≥3 placed) to close the hole; Esc / right-click cancels. Re-cutting replaces the
/// existing hole. Implements <see cref="IPreservesSelection"/> so activating it keeps the polygon selected
/// (the target). One hole per floor; a ring drawn outside the outline reads as no hole (see the primitive).
///
/// Editing a hole's corners with gizmos and removing a hole are a later slice — for now re-cut to adjust.
/// </summary>
public sealed class CutHoleTool : DrawToolBase, IPreservesSelection
{
    public override string Name => "Cut Hole";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private readonly List<Vector3> _points = new();   // world XZ at y = 0
    private readonly CornerMarkers _markers = new();

    public override void Activate(EditorContext ctx)
    {
        base.Activate(ctx);
        if (Target() == null)
            GD.Print("[cut hole] select a polygon floor first, then press the cut-hole tool and click corners inside it");
    }

    protected override void ResetState()
    {
        _points.Clear();
        _markers.Hide();
    }

    public override void OnClick()
    {
        if (Target() == null) return;
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        var p = new Vector3(corner.Value.X, 0, corner.Value.Z);

        if (_points.Count >= 3 && p.DistanceTo(_points[0]) < 0.001f) { Commit(); return; }
        if (_points.Count > 0 && p.DistanceTo(_points[^1]) < 0.001f) return; // ignore zero-length segment
        _points.Add(p);
    }

    public override void UpdatePreview()
    {
        PrimitiveInstanceData target = Target();
        if (target == null) { HidePreview(); _markers.Hide(); return; }

        Vector3 origin = target.LocalTransform.Origin;
        var world = new Transform3D(Basis.Identity, origin + Ctx.OffsetOfInstance(Ctx.SelectedId));

        var markerPts = new List<Vector3>(_points.Count);
        foreach (Vector3 p in _points) markerPts.Add(new Vector3(p.X, world.Origin.Y, p.Z));
        _markers.Show(Ctx.PreviewLayer, markerPts);

        // Ring being drawn = placed points + the hovered corner (unless it's on the first/last point).
        var ring = new List<Vector3>(_points);
        if (Ctx.Cursor.HoveredCorner is Vector3 hov)
        {
            var h = new Vector3(hov.X, 0, hov.Z);
            bool onFirst = _points.Count >= 3 && h.DistanceTo(_points[0]) < 0.001f;
            if (!onFirst && (_points.Count == 0 || h.DistanceTo(_points[^1]) > 0.001f)) ring.Add(h);
        }

        // Throwaway instance (never mutate the live target): the target's outline + the in-progress hole.
        // A <3-point or invalid hole is ignored by the primitive → the slab previews solid, never blanks.
        var preview = new PrimitiveInstanceData
        {
            PrimitiveType = "polygon_floor",
            LocalTransform = target.LocalTransform,
            Parameters = new Dictionary
            {
                { "points", ReadArray(target, "points") },
                { "hole", ToLocal(ring, origin) },
                { "thickness", target.Parameters.ContainsKey("thickness") ? target.Parameters["thickness"] : 0.2 },
            },
        };
        ShowPreview(Ctx.Registry.Get("polygon_floor").BuildMesh(preview, Ctx.BuildCtx()), world);
    }

    private void Commit()
    {
        PrimitiveInstanceData target = Target();
        if (target != null && _points.Count >= 3)
            Ctx.SetPolygonHole(Ctx.SelectedId, ToLocal(_points, target.LocalTransform.Origin));
        ResetState();
        HidePreview();
    }

    /// <summary>The selected polygon floor, or null (nothing selected / not a polygon floor).</summary>
    private PrimitiveInstanceData Target()
    {
        PrimitiveInstanceData inst = Ctx.GetInstance(Ctx.SelectedId);
        return inst != null && inst.PrimitiveType == "polygon_floor" ? inst : null;
    }

    private static Array<Vector3> ToLocal(List<Vector3> worldPts, Vector3 origin)
    {
        var arr = new Array<Vector3>();
        foreach (Vector3 p in worldPts) arr.Add(new Vector3(p.X - origin.X, 0, p.Z - origin.Z));
        return arr;
    }

    private static Array<Vector3> ReadArray(PrimitiveInstanceData inst, string key)
    {
        var arr = new Array<Vector3>();
        if (inst.Parameters.ContainsKey(key))
            foreach (Variant v in inst.Parameters[key].AsGodotArray()) arr.Add(v.AsVector3());
        return arr;
    }
}
