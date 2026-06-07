using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a banked curve: first click sets the entry corner (the curve's local origin), the second
/// sets the initial heading — the curve leaves the entry point along start→end and turns left from
/// there. The drag distance sets the turn <c>radius</c> (clamped); arc, width, bank and thickness
/// take their defaults and are tuned afterwards in the inspector. There is no rotate gizmo, so the
/// heading is fixed at draw time, exactly like the ramp/stairs tools.
/// </summary>
public sealed class BankedCurveDrawTool : DrawToolBase
{
    private const float MinRadius = 1f;
    private const float MaxRadius = 100f;

    public override string Name => "Banked Curve";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        if (_start == null) { _start = corner; return; }

        PrimitiveInstanceData curve = BuildCurve(_start.Value, corner.Value);
        if (curve != null) Ctx.AddInstance(curve);
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCorner == null) return;

        PrimitiveInstanceData inst = BuildCurve(_start.Value, Ctx.Cursor.HoveredCorner.Value);
        if (inst == null) { HidePreview(); return; }

        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("banked_curve").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildCurve(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.Y = 0;
        float dist = d.Length();
        if (dist < 0.001f) return null;

        float radius = Mathf.Clamp(dist, MinRadius, MaxRadius);
        float angle = Mathf.Atan2(-d.Z, d.X);     // rotate local +X onto the entry heading
        var basis = new Basis(Vector3.Up, angle);
        var origin = new Vector3(a.X, 0, a.Z);     // entry corner is the curve's local origin

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "banked_curve",
            LocalTransform = new Transform3D(basis, origin),
            Parameters = new Dictionary
            {
                { "radius", (double)radius },
                { "arc", 90.0 },
                { "width", (double)(Ctx.Document.Grid.CellSize * 2f) },
                { "bank", 0.0 },
                { "thickness", 0.2 },
                { "segments", 16 },
            },
        };
    }
}
