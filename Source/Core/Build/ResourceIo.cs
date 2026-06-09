using Godot;

namespace LevelBuilder.Core.Build;

/// <summary>
/// Saves/loads Godot resources to/from paths that may be <b>outside</b> the running project
/// (the workspace folder, the target game project).
///
/// Why this exists: Godot's text resource writer drops <c>res://</c> <c>ext_resource</c> lines —
/// including the <b>script</b> reference — when the destination file is outside the project, yielding
/// a scriptless, empty <c>.tres</c> (a <c>LevelDocument</c> comes back as a bare <see cref="Resource"/>).
/// So we always serialize to a <c>user://</c> temp (where <c>res://</c> refs are written correctly),
/// then byte-copy the file to the external destination. Loading mirrors it: copy the external file into
/// a <c>user://</c> temp and load from there, so the <c>res://</c> script refs inside resolve against the
/// running project. In-engine paths (<c>res://</c>/<c>user://</c>) skip the dance and save/load directly.
/// </summary>
public static class ResourceIo
{
    public static bool IsEnginePath(string path) =>
        path.StartsWith("res://") || path.StartsWith("user://");

    /// <summary>Saves <paramref name="res"/> to <paramref name="path"/> (engine or external). Returns the Error.
    /// The destination directory must already exist (callers EnsureDir).</summary>
    public static Error SaveTo(Resource res, string path)
    {
        if (IsEnginePath(path)) return ResourceSaver.Save(res, path);

        string tmp = $"user://__lb_tmp_save.{path.GetExtension()}";
        Error e = ResourceSaver.Save(res, tmp);
        if (e != Error.Ok) return e;
        return DirAccess.CopyAbsolute(tmp, path);
    }

    /// <summary>Loads a resource from <paramref name="path"/> (engine or external), fresh (CacheMode.Ignore).
    /// Returns null on failure (never throws).</summary>
    public static Resource LoadFrom(string path)
    {
        string load = path;
        if (!IsEnginePath(path))
        {
            load = $"user://__lb_tmp_load.{path.GetExtension()}";
            Error e = DirAccess.CopyAbsolute(path, load);
            if (e != Error.Ok)
            {
                GD.PrintErr($"[load] could not stage {path} ({e})");
                return null;
            }
        }
        return ResourceLoader.Load(load, null, ResourceLoader.CacheMode.Ignore);
    }
}
