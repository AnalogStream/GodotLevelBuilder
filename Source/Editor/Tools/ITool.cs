using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.Editor.Tools;

/// <summary>An interaction mode (select, draw floor, draw wall, ...).</summary>
public interface ITool
{
    string Name { get; }
    /// <summary>Snap mode this tool wants the grid cursor in.</summary>
    GridSnapMode SnapMode { get; }
    /// <summary>Whether the grid cell/corner cursor should be shown while this tool is active.</summary>
    bool UsesGridCursor { get; }

    void Activate(EditorContext ctx);
    void Deactivate();

    /// <summary>Primary (left) click at the current cursor position.</summary>
    void OnClick();
    /// <summary>Esc / right click — cancel the in-progress action.</summary>
    void OnCancel();
    /// <summary>Per-frame, for rubber-band previews.</summary>
    void UpdatePreview();
}
