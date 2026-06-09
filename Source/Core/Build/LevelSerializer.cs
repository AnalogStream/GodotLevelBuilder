using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Build;

/// <summary>Saves/loads a LevelDocument to/from a .tres (the editable source). Routes through
/// <see cref="ResourceIo"/> so saving to a workspace folder OUTSIDE the project keeps the script
/// references (a direct external save drops them — see ResourceIo).</summary>
public static class LevelSerializer
{
    public static Error Save(LevelDocument doc, string path) => ResourceIo.SaveTo(doc, path);

    /// <summary>Loads fresh off disk (CacheMode.Ignore). Returns null if the file isn't a
    /// LevelDocument (e.g. a legacy scriptless save) instead of throwing.</summary>
    public static LevelDocument Load(string path) => ResourceIo.LoadFrom(path) as LevelDocument;
}
