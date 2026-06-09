using System.Collections.Generic;
using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>One pickable texture from the asset pool: a res:// path plus how to group/label it.</summary>
public readonly record struct TextureItem(string Path, string Group, string Name);

/// <summary>
/// Discovers the raw textures the builder can paint with — the bundled Kenney prototype pack
/// (res://Assets/kenney_prototype_textures/&lt;color&gt;/texture_NN.png) plus any the user has added
/// (the workspace <c>textures/</c> folder) — and turns a chosen texture into a stable
/// <see cref="MaterialLibrary"/> entry so instances can reference it by id.
///
/// User textures are stored by their <em>workspace-relative</em> path (<c>textures/foo.png</c>) so a
/// level's .tres survives the workspace folder being moved; the bundled pack keeps its res:// path.
/// <see cref="Workspace.ResolveTexture"/> (via <see cref="TextureLoader"/>) resolves either form.
/// </summary>
public static class TextureCatalog
{
    public const string Root = "res://Assets/kenney_prototype_textures";

    /// <summary>Workspace-relative folder (under <see cref="Workspace.Root"/>) holding user-added textures.</summary>
    public const string UserRel = "textures";

    private static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tga" };

    /// <summary>All known textures: the bundled pack (grouped by color) then user-added (grouped "custom").</summary>
    public static List<TextureItem> Load()
    {
        var items = new List<TextureItem>();
        LoadPack(items);
        LoadUser(items);
        return items;
    }

    private static void LoadPack(List<TextureItem> items)
    {
        using DirAccess dir = DirAccess.Open(Root);
        if (dir == null) return;

        foreach (string sub in dir.GetDirectories())
        {
            using DirAccess colorDir = DirAccess.Open($"{Root}/{sub}");
            if (colorDir == null) continue;
            foreach (string file in colorDir.GetFiles())
                if (IsImage(file))
                    items.Add(new TextureItem($"{Root}/{sub}/{file}", sub, file));
        }
    }

    private static void LoadUser(List<TextureItem> items)
    {
        if (!Workspace.IsSet) return; // no workspace chosen yet
        using DirAccess dir = DirAccess.Open(Workspace.TexturesDir);
        if (dir == null) return; // folder only exists once the user has added something

        foreach (string file in dir.GetFiles())
            if (IsImage(file))
                items.Add(new TextureItem($"{UserRel}/{file}", "custom", file)); // workspace-relative
    }

    private static bool IsImage(string file)
    {
        string lower = file.ToLowerInvariant();
        foreach (string ext in ImageExts)
            if (lower.EndsWith(ext)) return true;
        return false;
    }

    /// <summary>
    /// Copies a user-chosen image (an OS-absolute path from the file dialog) into the workspace
    /// <c>textures/</c> folder so it gets a stable, portable path. Returns the workspace-relative
    /// destination (<c>textures/foo.png</c>), or "" on failure (incl. no workspace chosen).
    /// Overwrites an existing file of the same name (re-adding the same texture is idempotent).
    /// </summary>
    public static string ImportUserTexture(string sourcePath)
    {
        if (!Workspace.IsSet)
        {
            GD.PushWarning("[texture] no workspace set — choose a workspace before adding textures.");
            return "";
        }

        Error mk = DirAccess.MakeDirRecursiveAbsolute(Workspace.TexturesDir);
        if (mk != Error.Ok && mk != Error.AlreadyExists)
        {
            GD.PushWarning($"[texture] could not create {Workspace.TexturesDir}: {mk}");
            return "";
        }

        string file = sourcePath.GetFile();
        Error e = DirAccess.CopyAbsolute(sourcePath, $"{Workspace.TexturesDir}/{file}");
        if (e != Error.Ok)
        {
            GD.PrintErr($"[texture] copy failed ({e}): {sourcePath}");
            return "";
        }
        GD.Print($"[texture] added {Workspace.TexturesDir}/{file}");
        return $"{UserRel}/{file}";
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
