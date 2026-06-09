using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws a stair plane (a thick stepped slab, no base) between two grid corners: first click sets the
/// bottom (front) end, second the top (back). Total rise defaults to the active storey's height, the
/// step count is derived from that rise (~0.18 m per step), width to one grid cell, thickness to a thin
/// slab; all adjustable afterwards via gizmos. Mirrors <see cref="StairsDrawTool"/> but emits the
/// "stair_plane" primitive.
/// </summary>
public sealed class StairPlaneDrawTool : DrawToolBase
{
    private const float MinLength = 0.001f;
    private const float TargetRiser = 0.18f; // comfortable step height → drives the default step count

    public override string Name => "Stair Plane";
    public override GridSnapMode SnapMode => GridSnapMode.Corner;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? corner = Ctx.Cursor.HoveredCorner;
        if (corner == null) return;
        if (_start == null) { _start = corner; return; }

        PrimitiveInstanceData stairs = BuildStairs(_start.Value, corner.Value);
        if (stairs != null) Ctx.AddInstance(stairs);
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCorner == null) return;

        PrimitiveInstanceData inst = BuildStairs(_start.Value, Ctx.Cursor.HoveredCorner.Value);
        if (inst == null) { HidePreview(); return; }

        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("stair_plane").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildStairs(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.Y = 0;
        float run = d.Length();
        if (run < MinLength) return null;

        float rise = Ctx.DefaultStoreyHeight;
        int steps = Mathf.Max(1, Mathf.RoundToInt(rise / TargetRiser));

        float width = Ctx.Document.Grid.CellSize; // one cell wide → edges land on grid lines
        float angle = Mathf.Atan2(-d.Z, d.X);     // rotate local +X onto the run direction
        var basis = new Basis(Vector3.Up, angle);
        var mid = new Vector3((a.X + b.X) * 0.5f, 0, (a.Z + b.Z) * 0.5f);
        // Width is centred on the origin, so shift perpendicular by half-width: the drawn line becomes
        // the flight's near EDGE (it sits on the adjacent tiles), not its straddled centreline.
        mid += basis.Z * (width * 0.5f);

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "stair_plane",
            LocalTransform = new Transform3D(basis, mid),
            Parameters = new Dictionary
            {
                { "steps", steps },
                { "totalRise", (double)rise },
                { "run", (double)run },
                { "width", (double)width },
                { "thickness", 0.1 },
            },
        };
    }
}
