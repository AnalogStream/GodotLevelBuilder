using System;
using Godot;
using Godot.Collections;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Sets (or clears) a polygon floor's single hole ring (<c>Parameters["hole"]</c>) — one atomic, undoable
/// edit. An empty / &lt;3-point ring removes the key (solid slab). Snapshots are duplicated on apply so
/// undo/redo can't alias a live array.
/// </summary>
public sealed class SetHoleCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly Array<Vector3> _from, _to;
    private readonly Action _refresh;

    public SetHoleCommand(PrimitiveInstanceData instance, Array<Vector3> from, Array<Vector3> to, Action refresh)
    {
        _instance = instance;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => "Cut hole";

    public void Do() => Apply(_to);
    public void Undo() => Apply(_from);

    private void Apply(Array<Vector3> hole)
    {
        if (hole != null && hole.Count >= 3) _instance.Parameters["hole"] = hole.Duplicate();
        else _instance.Parameters.Remove("hole");
        _refresh();
    }
}
