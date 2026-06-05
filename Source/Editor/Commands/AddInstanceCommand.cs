using System;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>Adds a primitive instance to a storey (undo removes it). Refreshes the view after either.</summary>
public sealed class AddInstanceCommand : ICommand
{
    private readonly StoreyData _storey;
    private readonly PrimitiveInstanceData _instance;
    private readonly Action _refresh;

    public AddInstanceCommand(StoreyData storey, PrimitiveInstanceData instance, Action refresh)
    {
        _storey = storey;
        _instance = instance;
        _refresh = refresh;
    }

    public string Name => $"Add {_instance.PrimitiveType}";

    public void Do()
    {
        _storey.Instances.Add(_instance);
        _refresh();
    }

    public void Undo()
    {
        _storey.Instances.Remove(_instance);
        _refresh();
    }
}
