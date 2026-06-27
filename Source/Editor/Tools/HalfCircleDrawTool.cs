using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a half circle: first click sets the centre of the diameter (the local origin), the second a point on
/// the arc — the drag distance is the <c>radius</c> and the drag DIRECTION is where the arc bulges. Unlike the
/// disc/dome the half circle isn't rotationally symmetric, so the basis is rotated about Y to aim the local
/// +Z bulge along the drag (the diameter ends up perpendicular to it).
/// </summary>
public sealed class HalfCircleDrawTool : DrawToolBase
{
    private const float MinRadius = 0.1f;

    public override string Name => "Half Circle";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        if (_start == null) { _start = corner; return; }

        PrimitiveInstanceData inst = Build(_start.Value, corner.Value);
        if (inst != null) Ctx.AddInstance(inst);
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCorner == null) return;

        PrimitiveInstanceData inst = Build(_start.Value, Ctx.Cursor.HoveredCorner.Value);
        if (inst == null) { HidePreview(); return; }

        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("half_circle").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData Build(Vector3 centre, Vector3 rim)
    {
        Vector3 d = rim - centre;
        d.Y = 0;
        float radius = d.Length();
        if (radius < MinRadius) return null;

        // Aim local +Z (the bulge) along the drag: rotate about Y so (0,0,1) → normalized drag direction.
        Vector3 dir = d.Normalized();
        var basis = new Basis(Vector3.Up, Mathf.Atan2(dir.X, dir.Z));

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "half_circle",
            LocalTransform = new Transform3D(basis, new Vector3(centre.X, 0, centre.Z)),
            Parameters = new Dictionary
            {
                { "radius", (double)radius },
                { "sides", 24 },
                { "thickness", 0.2 },
            },
        };
    }
}
