using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws wall segments between grid corners. Corner mode: click a start corner, click the
/// next corner to place a wall, and the chain continues from that corner (click-click-click
/// to walk a perimeter). Esc / right-click ends the chain.
/// </summary>
public sealed class WallDrawTool : DrawToolBase
{
    private const float MinLength = 0.001f;

    public override string Name => "Wall";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;

        if (_start == null)
        {
            _start = corner;
            return;
        }

        PrimitiveInstanceData wall = BuildWall(_start.Value, corner.Value);
        if (wall != null) Ctx.AddInstance(wall);
        _start = corner; // chain from here
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
        ShowPreview(Ctx.Registry.Get("wall").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildWall(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.Y = 0;
        float length = d.Length();
        if (length < MinLength) return null;

        float angle = Mathf.Atan2(-d.Z, d.X); // rotate local +X onto the wall direction
        var mid = new Vector3((a.X + b.X) * 0.5f, 0, (a.Z + b.Z) * 0.5f);

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "wall",
            LocalTransform = new Transform3D(new Basis(Vector3.Up, angle), mid),
            Parameters = new Dictionary
            {
                { "length", (double)length },
                { "height", (double)Ctx.Storey.Height },
                { "thickness", 0.2 },
            },
        };
    }
}
