using Godot;
using Godot.Collections;

namespace LevelBuilder.Core.Data;

/// <summary>A vertical building layer (ground floor, 1st floor, ...).</summary>
[GlobalClass]
public partial class StoreyData : Resource
{
    /// <summary>Stable, unique within the level.</summary>
    [Export] public string Id { get; set; } = "";
    [Export] public string Name { get; set; } = "";
    /// <summary>Base height of this storey, metres.</summary>
    [Export] public float BaseElevation { get; set; }
    /// <summary>Storey height (default wall height), metres.</summary>
    [Export] public float Height { get; set; } = 3.0f;
    [Export] public Array<PrimitiveInstanceData> Instances { get; set; } = new();
}
