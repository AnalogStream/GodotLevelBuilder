using System;
using System.Collections.Generic;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>
/// Replaces an instance's material-slot assignments wholesale (e.g. painting every slot with a
/// dropped texture). Snapshots the prior slots on construction so Undo restores them exactly.
/// </summary>
public sealed class AssignMaterialCommand : ICommand
{
    private readonly PrimitiveInstanceData _inst;
    private readonly Dictionary<string, string> _from;
    private readonly Dictionary<string, string> _to;
    private readonly Action _refresh;

    public AssignMaterialCommand(PrimitiveInstanceData inst, Dictionary<string, string> to, Action refresh)
    {
        _inst = inst;
        _to = to;
        _from = Snapshot(inst.MaterialSlots); // current state = the pre-change state (Execute calls Do next)
        _refresh = refresh;
    }

    public string Name => "Assign material";

    public void Do() { Apply(_to); _refresh(); }
    public void Undo() { Apply(_from); _refresh(); }

    private void Apply(Dictionary<string, string> map)
    {
        _inst.MaterialSlots.Clear();
        foreach (KeyValuePair<string, string> kv in map) _inst.MaterialSlots[kv.Key] = kv.Value;
    }

    private static Dictionary<string, string> Snapshot(Godot.Collections.Dictionary slots)
    {
        var map = new Dictionary<string, string>();
        foreach (var key in slots.Keys) map[key.AsString()] = slots[key].AsString();
        return map;
    }
}
