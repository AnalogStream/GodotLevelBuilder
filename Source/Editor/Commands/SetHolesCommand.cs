using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Sets a polygon floor's hole set — the two parallel flat arrays <see cref="PolygonHoles.VertsKey"/> +
/// <see cref="PolygonHoles.SizesKey"/> (add / remove a hole, or clear all). One atomic, undoable edit;
/// snapshots are duplicated on apply so undo/redo can't alias a live array. Also clears the legacy single
/// "hole" key in BOTH directions, so a migrated floor never round-trips carrying both formats.
/// </summary>
public sealed class SetHolesCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly Array<Vector3> _fromVerts, _toVerts;
    private readonly Array<float> _fromSizes, _toSizes;
    private readonly Action _refresh;

    public SetHolesCommand(PrimitiveInstanceData instance,
        Array<Vector3> fromVerts, Array<float> fromSizes,
        Array<Vector3> toVerts, Array<float> toSizes, Action refresh)
    {
        _instance = instance;
        _fromVerts = fromVerts;
        _fromSizes = fromSizes;
        _toVerts = toVerts;
        _toSizes = toSizes;
        _refresh = refresh;
    }

    public string Name => "Edit holes";

    public void Do() => Apply(_toVerts, _toSizes);
    public void Undo() => Apply(_fromVerts, _fromSizes);

    private void Apply(Array<Vector3> verts, Array<float> sizes)
    {
        _instance.Parameters.Remove(PolygonHoles.LegacyKey); // migrate away from the single-hole format
        if (sizes.Count > 0)
        {
            _instance.Parameters[PolygonHoles.VertsKey] = verts.Duplicate();
            _instance.Parameters[PolygonHoles.SizesKey] = sizes.Duplicate();
        }
        else
        {
            _instance.Parameters.Remove(PolygonHoles.VertsKey);
            _instance.Parameters.Remove(PolygonHoles.SizesKey);
        }
        _refresh();
    }
}
