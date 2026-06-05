using System;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>The four editable scalars of an opening — snapshot for move/resize undo.</summary>
public readonly record struct OpeningState(float Offset, float Width, float Height, float Sill)
{
    public static OpeningState From(OpeningData o) => new(o.Offset, o.Width, o.Height, o.SillHeight);

    public void ApplyTo(OpeningData o)
    {
        o.Offset = Offset;
        o.Width = Width;
        o.Height = Height;
        o.SillHeight = Sill;
    }
}

/// <summary>
/// Moves or resizes an opening (offset / width / height / sill). The drag mutates the fields live;
/// this command formalizes the final state — Do re-asserts it (idempotent), Undo restores the
/// original. One command for every opening edit so they undo as a single atom.
/// </summary>
public sealed class EditOpeningCommand : ICommand
{
    private readonly OpeningData _opening;
    private readonly OpeningState _from, _to;
    private readonly Action _refresh;

    public EditOpeningCommand(OpeningData opening, OpeningState from, OpeningState to, Action refresh)
    {
        _opening = opening;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => "Edit opening";

    public void Do() { _to.ApplyTo(_opening); _refresh(); }
    public void Undo() { _from.ApplyTo(_opening); _refresh(); }
}
