using System;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Sets a single parameter on an instance (e.g. width/height/steps) from the inspector. Unlike
/// <see cref="ResizeInstanceCommand"/> — which anchors the opposite face and shifts the origin for a
/// gizmo drag — a direct property edit just rewrites the value and leaves the transform alone, so the
/// primitive resizes symmetrically about its local origin. The Variant is stored verbatim, preserving
/// the param's type (double vs int). Do/Undo simply swap the stored value.
/// </summary>
public sealed class SetParameterCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly string _key;
    private readonly Variant _from, _to;
    private readonly Action _refresh;

    public SetParameterCommand(PrimitiveInstanceData instance, string key, Variant from, Variant to, Action refresh)
    {
        _instance = instance;
        _key = key;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => $"Set {_instance.PrimitiveType} {_key}";

    public void Do() { _instance.Parameters[_key] = _to; _refresh(); }
    public void Undo() { _instance.Parameters[_key] = _from; _refresh(); }
}
