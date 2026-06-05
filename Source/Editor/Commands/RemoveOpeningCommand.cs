using System;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>Removes an opening (door/window hole) from a wall instance (undo restores it).</summary>
public sealed class RemoveOpeningCommand : ICommand
{
    private readonly PrimitiveInstanceData _wall;
    private readonly OpeningData _opening;
    private readonly Action _refresh;

    public RemoveOpeningCommand(PrimitiveInstanceData wall, OpeningData opening, Action refresh)
    {
        _wall = wall;
        _opening = opening;
        _refresh = refresh;
    }

    public string Name => "Remove opening";

    public void Do()
    {
        _wall.Openings.Remove(_opening);
        _refresh();
    }

    public void Undo()
    {
        _wall.Openings.Add(_opening);
        _refresh();
    }
}
