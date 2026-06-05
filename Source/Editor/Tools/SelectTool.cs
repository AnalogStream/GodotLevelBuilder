using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>
/// Click a placed primitive to select it (highlighted); clicking empty space clears the
/// selection. Delete removes the selected instance (handled by ToolManager). No grid cursor.
/// </summary>
public sealed class SelectTool : ITool
{
    private EditorContext _ctx;

    public string Name => "Select";
    public GridSnapMode SnapMode => GridSnapMode.Cell; // unused (cursor hidden)
    public bool UsesGridCursor => false;

    public void Activate(EditorContext ctx) => _ctx = ctx;
    public void Deactivate() { }

    public void OnClick() => _ctx.PickAndSelect();
    public void OnCancel() => _ctx.ClearSelection();
    public void UpdatePreview() { }
}
