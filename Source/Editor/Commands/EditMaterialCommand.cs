using System;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Editor.Commands;

/// <summary>The editable, per-texture render properties of a <see cref="MaterialEntry"/>.</summary>
public readonly record struct MaterialProps(float UvScale, Color Tint, bool Pixelated, int PixelSize);

/// <summary>
/// Edits a texture's shared properties (tiling + tint) on its <see cref="MaterialEntry"/>. Because a
/// texture is one library entry referenced by every instance that uses it, this affects all of them
/// (conventional shared-material semantics). The refresh callback must also evict the resolver's cached
/// build for this id (see <c>LevelView.InvalidateMaterial</c>) or the viewport keeps the stale material.
/// </summary>
public sealed class EditMaterialCommand : ICommand
{
    private readonly MaterialEntry _entry;
    private readonly MaterialProps _from, _to;
    private readonly Action _refresh;

    public EditMaterialCommand(MaterialEntry entry, MaterialProps from, MaterialProps to, Action refresh)
    {
        _entry = entry;
        _from = from;
        _to = to;
        _refresh = refresh;
    }

    public string Name => $"Edit texture {_entry.Id}";

    public void Do() { Apply(_to); _refresh(); }
    public void Undo() { Apply(_from); _refresh(); }

    private void Apply(MaterialProps p)
    {
        _entry.UvScale = p.UvScale;
        _entry.Tint = p.Tint;
        _entry.Pixelated = p.Pixelated;
        _entry.PixelSize = p.PixelSize;
    }
}
