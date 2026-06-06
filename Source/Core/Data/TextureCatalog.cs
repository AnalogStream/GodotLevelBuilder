using System.Collections.Generic;
using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>One pickable texture from the asset pool: a res:// path plus how to group/label it.</summary>
public readonly record struct TextureItem(string Path, string Group, string Name);

/// <summary>
/// Discovers the raw textures the builder can paint with (the Kenney prototype pack:
/// res://Assets/kenney_prototype_textures/&lt;color&gt;/texture_NN.png), and turns a chosen
/// texture into a stable <see cref="MaterialLibrary"/> entry so instances can reference it by id.
/// </summary>
public static class TextureCatalog
{
    public const string Root = "res://Assets/kenney_prototype_textures";

    /// <summary>All textures under the pack, grouped by their color subfolder. Empty if the folder is missing.</summary>
    public static List<TextureItem> Load()
    {
        var items = new List<TextureItem>();
        using DirAccess dir = DirAccess.Open(Root);
        if (dir == null) return items;

        foreach (string sub in dir.GetDirectories())
        {
            using DirAccess colorDir = DirAccess.Open($"{Root}/{sub}");
            if (colorDir == null) continue;
            foreach (string file in colorDir.GetFiles())
                if (file.EndsWith(".png"))
                    items.Add(new TextureItem($"{Root}/{sub}/{file}", sub, file));
        }
        return items;
    }

    /// <summary>Stable library id for a texture path (so re-applying the same texture reuses one entry).</summary>
    public static string IdFor(string texturePath) => $"tex:{texturePath}";

    /// <summary>
    /// Ensures <paramref name="library"/> has an entry for <paramref name="texturePath"/> and returns its id.
    /// Idempotent: the same texture always maps to the same entry.
    /// </summary>
    public static string EnsureEntry(MaterialLibrary library, string texturePath)
    {
        string id = IdFor(texturePath);
        if (library.Find(id) == null)
            library.Entries.Add(new MaterialEntry
            {
                Id = id,
                DisplayName = texturePath.GetFile(), // e.g. "texture_03.png"
                TexturePath = texturePath,
            });
        return id;
    }
}
