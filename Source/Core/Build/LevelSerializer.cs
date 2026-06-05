using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Build;

/// <summary>Saves/loads a LevelDocument to/from a .tres (the editable source).</summary>
public static class LevelSerializer
{
    public static Error Save(LevelDocument doc, string path)
    {
        return ResourceSaver.Save(doc, path);
    }

    /// <summary>
    /// Loads with CacheMode.Ignore so we get a fresh instance off disk — essential for
    /// the round-trip test, otherwise the loader returns the still-in-memory document.
    /// </summary>
    public static LevelDocument Load(string path)
    {
        return ResourceLoader.Load<LevelDocument>(path, null, ResourceLoader.CacheMode.Ignore);
    }
}
