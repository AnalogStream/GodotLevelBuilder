using System;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Moves a primitive instance from <c>from</c> to <c>to</c> (its <see cref="PrimitiveInstanceData.LocalTransform"/>).
/// The drag mutates the transform live for feedback; this command formalizes the final placement so
/// undo/redo is correct — Do re-asserts <c>to</c> (idempotent), Undo restores <c>from</c>.
/// </summary>
public sealed class MoveInstanceCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly Transform3D _from;
    private readonly Transform3D _to;
    private readonly Action _refresh;

    public MoveInstanceCommand(PrimitiveInstanceData instance, Transform3D from, Transform3D to, Action refresh)
    {
        _instance = instance;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => $"Move {_instance.PrimitiveType}";

    public void Do()
    {
        _instance.LocalTransform = _to;
        _refresh();
    }

    public void Undo()
    {
        _instance.LocalTransform = _from;
        _refresh();
    }
}
