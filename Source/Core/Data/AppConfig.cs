using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>
/// Persistent app settings, stored as a <see cref="ConfigFile"/> in a <c>Settings/</c> folder right
/// next to the executable, so each install carries its own isolated config (portable-app style) rather
/// than sharing the global <c>user://</c> dir with the editor and other copies. Holds the pointer to
/// the chosen workspace folder, the target game project to export into, and the last opened level (for
/// resume-on-launch). The workspace folder itself holds the actual levels and textures; this file just
/// remembers where it is.
/// </summary>
public sealed class AppConfig
{
    private const string Section = "workspace";

    /// <summary>
    /// Resolves the config path next to the running binary. When launched from the Godot editor
    /// <see cref="OS.GetExecutablePath"/> points at the editor binary, so we fall back to the project
    /// folder there to avoid littering the editor install during development.
    /// </summary>
    private static string FilePath
    {
        get
        {
            string baseDir = OS.HasFeature("editor")
                ? ProjectSettings.GlobalizePath("res://")
                : OS.GetExecutablePath().GetBaseDir();
            return $"{baseDir}/Settings/levelbuilder.cfg";
        }
    }

    private readonly ConfigFile _cfg = new();

    /// <summary>Absolute OS path to the workspace folder (levels + textures live here).</summary>
    public string WorkspacePath { get; set; } = "";
    /// <summary>Absolute OS path to the target Godot game project root that baked levels export into.</summary>
    public string TargetProjectPath { get; set; } = "";
    /// <summary>Absolute OS path to the last opened level .tres, reopened on launch.</summary>
    public string LastLevelPath { get; set; } = "";

    public bool HasWorkspace => !string.IsNullOrEmpty(WorkspacePath);
    public bool HasTarget => !string.IsNullOrEmpty(TargetProjectPath);

    public static AppConfig Load()
    {
        var c = new AppConfig();
        if (c._cfg.Load(FilePath) == Error.Ok)
        {
            c.WorkspacePath = c._cfg.GetValue(Section, "workspace_path", "").AsString();
            c.TargetProjectPath = c._cfg.GetValue(Section, "target_project", "").AsString();
            c.LastLevelPath = c._cfg.GetValue(Section, "last_level", "").AsString();
        }
        return c;
    }

    public void Save()
    {
        _cfg.SetValue(Section, "workspace_path", WorkspacePath);
        _cfg.SetValue(Section, "target_project", TargetProjectPath);
        _cfg.SetValue(Section, "last_level", LastLevelPath);

        string path = FilePath;
        DirAccess.MakeDirRecursiveAbsolute(path.GetBaseDir());
        Error e = _cfg.Save(path);
        if (e != Error.Ok) GD.PushWarning($"[config] save failed ({e})");
    }
}
