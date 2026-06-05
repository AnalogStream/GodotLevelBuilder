using Godot;
using Godot.Collections;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Draws rectangular floors. Cell mode: the first click sets the start cell, the second
/// click commits a floor covering the inclusive bounding rectangle of the two cells.
/// (Clicking the same cell twice makes a 1×1 floor; a single click alone draws nothing yet.)
/// </summary>
public sealed class FloorDrawTool : DrawToolBase
{
    public override string Name => "Floor";
    public override GridSnapMode SnapMode => GridSnapMode.Cell;

    private Vector3? _start;

    protected override void ResetState() => _start = null;

    public override void OnClick()
    {
        Vector3? cell = Ctx.Cursor.HoveredCell;
        if (cell == null) return;

        if (_start == null)
        {
            _start = cell;
            return;
        }

        Ctx.AddInstance(BuildFloor(_start.Value, cell.Value));
        _start = null;
        HidePreview();
    }

    public override void UpdatePreview()
    {
        if (_start == null) { HidePreview(); return; }
        if (Ctx.Cursor.HoveredCell == null) return; // keep last preview while off-grid

        PrimitiveInstanceData inst = BuildFloor(_start.Value, Ctx.Cursor.HoveredCell.Value);
        Transform3D world = inst.LocalTransform;
        world.Origin += Ctx.ElevationOffset;
        ShowPreview(Ctx.Registry.Get("floor").BuildMesh(inst, Ctx.BuildCtx()), world);
    }

    private PrimitiveInstanceData BuildFloor(Vector3 cellA, Vector3 cellB)
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
            PrimitiveType = "floor",
            LocalTransform = new Transform3D(Basis.Identity, center),
            Parameters = new Dictionary
            {
                { "width", (double)(maxX - minX) },
                { "depth", (double)(maxZ - minZ) },
                { "thickness", 0.2 },
            },
        };
    }
}
