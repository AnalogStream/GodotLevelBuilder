using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws an edge curb / rim. Like the floor tool: first click sets the start cell, the second commits a
/// curb framing the inclusive bounding rectangle of the two cells. Rail height and thickness take their
/// defaults and are tuned afterwards in the inspector / via gizmos.
/// </summary>
public sealed class EdgeCurbDrawTool : DrawToolBase
{
    public override string Name => "Edge Curb";
    public override GridSnapMode SnapMode => GridSnapMode.Cell;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? cell = Ctx.Cursor.HoveredCell;
        if (cell == null) return;
        if (_start == null) { _start = cell; return; }

        Ctx.AddInstance(BuildCurb(_start.Value, cell.Value));
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCell == null) return;

        PrimitiveInstanceData inst = BuildCurb(_start.Value, Ctx.Cursor.HoveredCell.Value);
        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("edge_curb").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildCurb(Vector3 cellA, Vector3 cellB)
    {
        float cs = Ctx.Document.Grid.CellSize;
        float minX = Mathf.Min(cellA.X, cellB.X);
        float maxX = Mathf.Max(cellA.X, cellB.X) + cs;
        float minZ = Mathf.Min(cellA.Z, cellB.Z);
        float maxZ = Mathf.Max(cellA.Z, cellB.Z) + cs;

        var center = new Vector3((minX + maxX) * 0.5f, 0, (minZ + maxZ) * 0.5f);

        return new PrimitiveInstanceData
        {
            Id = Ids.New(),
            PrimitiveType = "edge_curb",
            LocalTransform = new Transform3D(Basis.Identity, center),
            Parameters = new Dictionary
            {
                { "width", (double)(maxX - minX) },
                { "depth", (double)(maxZ - minZ) },
                { "railHeight", 0.3 },
                { "thickness", 0.15 },
            },
        };
    }
}
