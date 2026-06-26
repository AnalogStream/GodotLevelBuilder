namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// Marks a handle that, when clicked, selects a path control point rather than starting a drag. The
/// click is transient view state (which point is active), not a document edit, so <c>SelectTool</c>
/// intercepts it before the generic command-driven drag path and routes it to
/// <c>EditorContext.SelectPathPoint</c> — mirroring how an opening sub-selection works.
/// </summary>
public interface IPathPointSelect
{
    /// <summary>Index of the control point this marker represents.</summary>
    int Index { get; }
}
