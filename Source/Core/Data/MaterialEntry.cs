using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>
/// One named material the builder can assign to a primitive's slot.
/// NOTE: must live in its own file named exactly MaterialEntry.cs — Godot can only
/// reconstruct a [GlobalClass] C# Resource from a .tres when the class name matches
/// the filename. (Learned the hard way in Milestone 1.)
/// </summary>
[GlobalClass]
public partial class MaterialEntry : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    /// <summary>res:// path to a Material resource (.tres / .material). Takes priority over <see cref="TexturePath"/>.</summary>
    [Export] public string MaterialPath { get; set; } = "";
    /// <summary>
    /// res:// path to a raw Texture2D. When <see cref="MaterialPath"/> is empty, the resolver builds a
    /// StandardMaterial3D with this as its albedo — so picking a texture needs no on-disk .material file.
    /// </summary>
    [Export] public string TexturePath { get; set; } = "";
}
