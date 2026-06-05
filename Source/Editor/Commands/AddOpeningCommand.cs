using System;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>Adds an opening (door/window hole) to a wall instance (undo removes it).</summary>
public sealed class AddOpeningCommand : ICommand
{
    private readonly PrimitiveInstanceData _wall;
    private readonly OpeningData _opening;
    private readonly Action _refresh;

    public AddOpeningCommand(PrimitiveInstanceData wall, OpeningData opening, Action refresh)
    {
        _wall = wall;
        _opening = opening;
        _refresh = refresh;
    }

    public string Name => "Add opening";

    public void Do()
    {
        _wall.Openings.Add(_opening);
        _refresh();
    }

    public void Undo()
    {
        _wall.Openings.Remove(_opening);
        _refresh();
    }
}
