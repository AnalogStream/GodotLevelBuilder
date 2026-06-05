using System;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Moves an opening along its wall, from <c>from</c> to <c>to</c> (its <see cref="OpeningData.Offset"/>).
/// The drag mutates the offset live for feedback; this command formalizes the final position so
/// undo/redo is correct — Do re-asserts <c>to</c> (idempotent), Undo restores <c>from</c>.
/// </summary>
public sealed class MoveOpeningCommand : ICommand
{
    private readonly OpeningData _opening;
    private readonly float _from;
    private readonly float _to;
    private readonly Action _refresh;

    public MoveOpeningCommand(OpeningData opening, float from, float to, Action refresh)
    {
        _opening = opening;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => "Move opening";

    public void Do()
    {
        _opening.Offset = _to;
        _refresh();
    }

    public void Undo()
    {
        _opening.Offset = _from;
        _refresh();
    }
}
