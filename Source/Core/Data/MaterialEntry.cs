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

    /// <summary>
    /// Texture tiling: a multiplier on the (world-unit) UVs, so higher = the texture repeats more often
    /// (smaller tiles). Applied only to texture-built materials (<see cref="TexturePath"/>), not loaded
    /// .material files. Default 1. (Tiling needs the texture's wrap=repeat; imported textures default to
    /// it — a just-added user texture may clamp until the editor reimports it. See docs/EXPORT.md.)
    /// </summary>
    [Export] public float UvScale { get; set; } = 1f;

    /// <summary>Albedo tint multiplied onto a texture-built material. Default white (no change).</summary>
    [Export] public Color Tint { get; set; } = Colors.White;

    /// <summary>
    /// When true the texture is downsampled to <see cref="PixelSize"/> texels and shown with a Nearest
    /// filter — a chunky pixel-art look (e.g. a photographic grass texture matched to a low-fi artstyle).
    /// Applied only to texture-built materials, like tiling/tint. Default off (full-res, smooth filtering).
    /// </summary>
    [Export] public bool Pixelated { get; set; } = false;

    /// <summary>
    /// Pixelation resolution: the texture's longest side in texels when <see cref="Pixelated"/> is on
    /// (aspect preserved; lower = chunkier pixels). Default 32.
    /// </summary>
    [Export] public int PixelSize { get; set; } = 32;
}
