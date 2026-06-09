using System.Collections.Generic;
using Godot;

namespace LevelBuilder.Editor.Commands;

/// <summary>Undo/redo stack. Executing a new command clears the redo history.</summary>
public sealed class CommandStack
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Drops all undo/redo history (e.g. when a different document is opened).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Execute(ICommand command)
    {
        command.Do();
        _undo.Push(command);
        _redo.Clear();
        GD.Print($"[cmd] {command.Name}");
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        ICommand c = _undo.Pop();
        c.Undo();
        _redo.Push(c);
        GD.Print($"[undo] {c.Name}");
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        ICommand c = _redo.Pop();
        c.Do();
        _undo.Push(c);
        GD.Print($"[redo] {c.Name}");
    }
}
