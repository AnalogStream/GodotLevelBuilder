using System.Collections.Generic;
using Godot;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Undo/redo stack. Executing a new command clears the redo history.
///
/// Dirty tracking: the stack remembers WHICH command was on top at the last save (a reference
/// marker, not a counter — a counter would mis-report "clean" after undo-then-new-edit). The
/// document is dirty whenever the current top differs from that marker, so undoing back to the
/// save point correctly returns to clean. Known accepted gaps: document name and grid height-step
/// edits bypass the stack (metadata / view prefs, deliberately not undoable) and never dirty.
/// </summary>
public sealed class CommandStack
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();
    private ICommand _saveMarker; // top of _undo at last save; null = the empty (freshly loaded) state

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Fires whenever IsDirty may have changed (any stack mutation or save).</summary>
    public event System.Action DirtyChanged;

    /// <summary>True when the document has edits not yet saved.</summary>
    public bool IsDirty => _undo.Count == 0 ? _saveMarker != null : !ReferenceEquals(_undo.Peek(), _saveMarker);

    /// <summary>Call after a successful save: the current state becomes the clean baseline.</summary>
    public void MarkSaved()
    {
        _saveMarker = _undo.Count > 0 ? _undo.Peek() : null;
        DirtyChanged?.Invoke();
    }

    /// <summary>Drops all undo/redo history (e.g. when a different document is opened). The fresh
    /// document starts clean.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _saveMarker = null;
        DirtyChanged?.Invoke();
    }

    public void Execute(ICommand command)
    {
        command.Do();
        _undo.Push(command);
        _redo.Clear();
        GD.Print($"[cmd] {command.Name}");
        DirtyChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        ICommand c = _undo.Pop();
        c.Undo();
        _redo.Push(c);
        GD.Print($"[undo] {c.Name}");
        DirtyChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        ICommand c = _redo.Pop();
        c.Do();
        _undo.Push(c);
        GD.Print($"[redo] {c.Name}");
        DirtyChanged?.Invoke();
    }
}
