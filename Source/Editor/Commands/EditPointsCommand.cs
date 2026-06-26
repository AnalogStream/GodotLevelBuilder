using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Replaces an instance's control-point list (<c>Parameters["points"]</c>) — move / insert / remove a
/// point. Points-only, with NO parallel bank array (unlike <see cref="EditPathCommand"/>), for the
/// polygon floor whose outline is planar. Snapshots are duplicated on apply so undo/redo can't alias the
/// live array a handle keeps editing.
/// </summary>
public sealed class EditPointsCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly Array<Vector3> _from, _to;
    private readonly Action _refresh;

    public EditPointsCommand(PrimitiveInstanceData instance, Array<Vector3> from, Array<Vector3> to, Action refresh)
    {
        _instance = instance;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => "Edit polygon";

    public void Do() => Apply(_to);
    public void Undo() => Apply(_from);

    private void Apply(Array<Vector3> points)
    {
        _instance.Parameters["points"] = points.Duplicate();
        _refresh();
    }
}
