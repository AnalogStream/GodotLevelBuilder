namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Marker for a tool that must NOT clear the current selection when it is activated — it operates ON the
/// selected object (e.g. the cut-hole tool cuts a hole into the selected polygon floor). ToolManager
/// skips its usual <c>ClearSelection()</c> when switching to such a tool.
/// </summary>
public interface IPreservesSelection
{
}
