using System;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>Removes an instance from a storey (undo re-inserts it at its original index).</summary>
public sealed class RemoveInstanceCommand : ICommand
{
    private readonly StoreyData _storey;
    private readonly PrimitiveInstanceData _instance;
    private readonly int _index;
    private readonly Action _refresh;

    public RemoveInstanceCommand(StoreyData storey, PrimitiveInstanceData instance, int index, Action refresh)
    {
        _storey = storey;
        _instance = instance;
        _index = index;
        _refresh = refresh;
    }

    public string Name => $"Delete {_instance.PrimitiveType}";

    public void Do()
    {
        _storey.Instances.Remove(_instance);
        _refresh();
    }

    public void Undo()
    {
        int at = Mathf.Clamp(_index, 0, _storey.Instances.Count);
        _storey.Instances.Insert(at, _instance);
        _refresh();
    }
}
