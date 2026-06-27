using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a circle plane: first click sets the centre (the local origin), the second a point on the rim —
/// the drag distance is the <c>radius</c>. The disc is rotationally symmetric, so orientation doesn't
/// matter (identity basis, like the dome).
/// </summary>
public sealed class CirclePlaneDrawTool : DrawToolBase
{
    private const float MinRadius = 0.1f;

    public override string Name => "Circle Plane";
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
        ShowPreview(Ctx.Registry.Get("circle_plane").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData Build(Vector3 centre, Vector3 rim)
    {
        Vector3 d = rim - centre;
        d.Y = 0;
        float radius = d.Length();
        if (radius < MinRadius) return null;

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "circle_plane",
            LocalTransform = new Transform3D(Basis.Identity, new Vector3(centre.X, 0, centre.Z)),
            Parameters = new Dictionary
            {
                { "radius", (double)radius },
                { "sides", 32 },
                { "thickness", 0.2 },
            },
        };
    }
}
