namespace LevelBuilder.Editor.Session;

/// <summary>Severity of a user-facing notification raised by <see cref="EditorContext.Notified"/>.
/// Lives in the Editor layer (not UI) so the context never depends on UI types; the toast layer
/// maps these to colors.</summary>
public enum NotifyLevel
{
    Info,
    Success,
    Warning,
    Error,
}
