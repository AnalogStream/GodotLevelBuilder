using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a curved wall: first click sets the entry corner (the arc's local origin), the second sets the
/// initial heading — the wall leaves the entry along start→end and curves left from there. The drag
/// distance sets the <c>radius</c> (clamped); arc, height and thickness take defaults, tuned afterwards.
/// Mirrors the banked-curve tool, so a curved rail lines up with a curve drawn the same way.
/// </summary>
public sealed class CurvedWallDrawTool : DrawToolBase
{
    private const float MinRadius = 1f;
    private const float MaxRadius = 100f;

    public override string Name => "Curved Wall";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        if (_start == null) { _start = corner; return; }

        PrimitiveInstanceData inst = BuildWall(_start.Value, corner.Value);
        if (inst != null) Ctx.AddInstance(inst);
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCorner == null) return;

        PrimitiveInstanceData inst = BuildWall(_start.Value, Ctx.Cursor.HoveredCorner.Value);
        if (inst == null) { HidePreview(); return; }

        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("curved_wall").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildWall(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.Y = 0;
        float dist = d.Length();
        if (dist < 0.001f) return null;

        float radius = Mathf.Clamp(dist, MinRadius, MaxRadius);
        float angle = Mathf.Atan2(-d.Z, d.X);     // rotate local +X onto the entry heading
        var basis = new Basis(Vector3.Up, angle);
        var origin = new Vector3(a.X, 0, a.Z);

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "curved_wall",
            LocalTransform = new Transform3D(basis, origin),
            Parameters = new Dictionary
            {
                { "radius", (double)radius },
                { "arc", 90.0 },
                { "height", 1.0 },
                { "thickness", 0.2 },
                { "segments", 16 },
            },
        };
    }
}
