using System;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Resizes one dimension of an instance: a parameter (e.g. width/length/height) plus the transform,
/// since an anchored resize shifts the origin so the opposite face stays put. When a wall's length
/// resizes from its u=0 end, the openings measured from that end are shifted too (kept world-fixed),
/// so their offsets are snapshotted here and restored as one atom. The drag mutates everything live;
/// this command formalizes the final values — Do re-asserts them (idempotent), Undo restores originals.
/// </summary>
public sealed class ResizeInstanceCommand : ICommand
{
    private readonly PrimitiveInstanceData _instance;
    private readonly string _paramKey;
    private readonly float _fromValue, _toValue;
    private readonly Transform3D _fromXform, _toXform;
    private readonly OpeningData[] _openings;
    private readonly float[] _openingsFrom, _openingsTo;
    private readonly Action _refresh;

    public ResizeInstanceCommand(PrimitiveInstanceData instance, string paramKey,
        float fromValue, float toValue, Transform3D fromXform, Transform3D toXform,
        OpeningData[] openings, float[] openingsFrom, float[] openingsTo, Action refresh)
    {
        _instance = instance;
        _paramKey = paramKey;
        _fromValue = fromValue;
        _toValue = toValue;
        _fromXform = fromXform;
        _toXform = toXform;
        _openings = openings;
        _openingsFrom = openingsFrom;
        _openingsTo = openingsTo;
        _refresh = refresh;
    }

    public string Name => $"Resize {_instance.PrimitiveType} {_paramKey}";

    public void Do()
    {
        _instance.Parameters[_paramKey] = (double)_toValue;
        _instance.LocalTransform = _toXform;
        for (int i = 0; i < _openings.Length; i++) _openings[i].Offset = _openingsTo[i];
        _refresh();
    }

    public void Undo()
    {
        _instance.Parameters[_paramKey] = (double)_fromValue;
        _instance.LocalTransform = _fromXform;
        for (int i = 0; i < _openings.Length; i++) _openings[i].Offset = _openingsFrom[i];
        _refresh();
    }
}
