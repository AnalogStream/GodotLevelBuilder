using Godot;
using Godot.Collections;

namespace LevelBuilder.Core.Data;

/// <summary>
/// A hole punched in a wall (door / window). Owned by the wall's instance.
/// Unused by floors in Milestone 1, defined here so the data shape is stable.
/// See docs/PRIMITIVES.md for the box-decomposition the wall generator uses.
/// </summary>
[GlobalClass]
public partial class OpeningData : Resource
{
    [Export] public string Id { get; set; } = "";
    /// <summary>Distance along the wall, metres.</summary>
    [Export] public float Offset { get; set; }
    [Export] public float Width { get; set; } = 1.0f;
    [Export] public float Height { get; set; } = 2.0f;
    /// <summary>0 for doors, &gt;0 for windows.</summary>
    [Export] public float SillHeight { get; set; }
    /// <summary>Empty = bare hole, otherwise a frame primitive TypeId.</summary>
    [Export] public string FrameType { get; set; } = "";
    [Export] public Dictionary FrameMaterialSlots { get; set; } = new();
}
