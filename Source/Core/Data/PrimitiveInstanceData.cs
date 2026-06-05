using Godot;
using Godot.Collections;

namespace LevelBuilder.Core.Data;

/// <summary>
/// One placed primitive within a storey. Parameters and MaterialSlots are
/// untyped dictionaries so new primitive types need no data-model changes.
/// </summary>
[GlobalClass]
public partial class PrimitiveInstanceData : Resource
{
    /// <summary>Stable, unique within the level. Never reused. Drives bake node names + bindings.</summary>
    [Export] public string Id { get; set; } = "";
    /// <summary>PrimitiveRegistry key, e.g. "floor", "wall", "stairs".</summary>
    [Export] public string PrimitiveType { get; set; } = "";
    /// <summary>Placement within the storey (pre-snapped).</summary>
    [Export] public Transform3D LocalTransform { get; set; } = Transform3D.Identity;
    /// <summary>paramKey -> Variant value (see each primitive's ParamSpecs).</summary>
    [Export] public Dictionary Parameters { get; set; } = new();
    /// <summary>slotName -> material Id (into the level's MaterialLibrary).</summary>
    [Export] public Dictionary MaterialSlots { get; set; } = new();
    /// <summary>Walls only; empty otherwise.</summary>
    [Export] public Array<OpeningData> Openings { get; set; } = new();
}
