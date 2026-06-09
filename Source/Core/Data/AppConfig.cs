using Godot;

namespace LevelBuilder.Core.Data;

/// <summary>
/// Persistent app settings, stored as a <see cref="ConfigFile"/> at <c>user://levelbuilder.cfg</c> —
/// the one location that stays writable even when the builder is exported as a standalone binary.
/// Holds the pointer to the chosen workspace folder, the target game project to export into, and the
/// last opened level (for resume-on-launch). The workspace folder itself holds the actual levels and
/// textures; this file just remembers where it is.
/// </summary>
public sealed class AppConfig
{
    private const string FilePath = "user://levelbuilder.cfg";
    private const string Section = "workspace";

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
        Error e = _cfg.Save(FilePath);
        if (e != Error.Ok) GD.PushWarning($"[config] save failed ({e})");
    }
}
