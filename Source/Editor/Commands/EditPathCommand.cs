using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Replaces a path_sweep instance's control-point list AND the parallel per-point bank list (move /
/// insert / remove a point, or roll a point's bank). The two arrays must stay length-aligned, so they
/// are snapshotted and swapped together — one atomic, undoable edit. Snapshots are duplicated on apply
/// so undo/redo can't alias the live arrays a handle keeps editing.
/// </summary>
public sealed class EditPathCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly Array<Vector3> _fromPoints, _toPoints;
    private readonly Array<float> _fromBanks, _toBanks;
    private readonly Action _refresh;

    public EditPathCommand(PrimitiveInstanceData instance,
        Array<Vector3> fromPoints, Array<float> fromBanks,
        Array<Vector3> toPoints, Array<float> toBanks, Action refresh)
    {
        _instance = instance;
        _fromPoints = fromPoints;
        _fromBanks = fromBanks;
        _toPoints = toPoints;
        _toBanks = toBanks;
        _refresh = refresh;
    }

    public string Name => "Edit path";

    public void Do() => Apply(_toPoints, _toBanks);
    public void Undo() => Apply(_fromPoints, _fromBanks);

    private void Apply(Array<Vector3> points, Array<float> banks)
    {
        _instance.Parameters["points"] = points.Duplicate();
        _instance.Parameters["banks"] = banks.Duplicate();
        _refresh();
    }
}
