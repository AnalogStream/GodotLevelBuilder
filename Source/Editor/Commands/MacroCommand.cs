using System;
using System.Collections.Generic;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Bundles several child commands into one undoable unit (e.g. move every selected instance, or paint
/// every selected instance with one texture). Children are built with a no-op refresh and the macro
/// refreshes once after applying them all. Undo runs the children in reverse so layered edits unwind
/// in the right order.
/// </summary>
public sealed class MacroCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> _children;
    private readonly Action _refresh;

    public MacroCommand(string name, IReadOnlyList<ICommand> children, Action refresh)
    {
        Name = name;
        _children = children;
        _refresh = refresh;
    }

    public string Name { get; }

    public void Do()
    {
        foreach (ICommand c in _children) c.Do();
        _refresh();
    }

    public void Undo()
    {
        for (int i = _children.Count - 1; i >= 0; i--) _children[i].Undo();
        _refresh();
    }
}
