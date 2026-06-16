using Godot;
using LevelBuilder.Editor.Session;
using LevelBuilder.Editor.Tools;

namespace LevelBuilder.UI;

/// <summary>
/// Bottom status strip: active tool, draw height, selection count, and a right-aligned controls
/// hint. Subscribes to <see cref="EditorContext.Changed"/> (fired per drag frame — the work here is
/// just label text, cheap enough) and <see cref="ToolManager.ActiveToolIdChanged"/>.
/// </summary>
public partial class StatusBar : PanelContainer
{
    private EditorContext _ctx;
    private ToolManager _tools;
    private Label _tool;
    private Label _height;
    private Label _selection;

    public void Setup(EditorContext ctx, ToolManager tools)
    {
        _ctx = ctx;
        _tools = tools;

        var row = new HBoxContainer();
        AddChild(row);

        _tool = Cell("Tool: Select", "The active tool — switch in the Primitives palette or by hotkey.");
        row.AddChild(_tool);
        row.AddChild(new VSeparator());
        _height = Cell("Height: 0.00 m", "Draw-plane elevation (▲/▼ in the viewport corner, +/- for layers).");
        row.AddChild(_height);
        row.AddChild(new VSeparator());
        _selection = Cell("Nothing selected", "Ctrl+click to multi-select; Del deletes the selection.");
        row.AddChild(_selection);

        var hint = new Label
        {
            Text = "LMB draw/select  ·  MMB orbit  ·  Shift+MMB pan  ·  wheel zoom  ·  7 top-down  ·  F1 help",
            Modulate = UiConstants.FontDim,
            HorizontalAlignment = HorizontalAlignment.Right,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        row.AddChild(hint);

        _ctx.Changed += Refresh;
        _tools.ActiveToolIdChanged += OnToolChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_ctx != null) _ctx.Changed -= Refresh;
        if (_tools != null) _tools.ActiveToolIdChanged -= OnToolChanged;
    }

    private static Label Cell(string text, string tooltip) => new()
    {
        Text = text,
        TooltipText = tooltip,
        MouseFilter = MouseFilterEnum.Stop, // so the tooltip shows
    };

    private void OnToolChanged(string id)
    {
        string name = id == null ? "Select" : $"{char.ToUpperInvariant(id[0])}{id[1..].Replace('_', ' ')}";
        string hotkey = id != null ? _tools.HotkeyFor(id) : "S";
        _tool.Text = hotkey != null ? $"Tool: {name} ({hotkey})" : $"Tool: {name}";
    }

    private void Refresh()
    {
        _height.Text = $"Height: {_ctx.DrawHeight:0.00} m";
        int n = _ctx.SelectedIds.Count;
        _selection.Text = _ctx.SelectedOpeningId != null ? "Opening selected"
            : n == 0 ? "Nothing selected"
            : n == 1 ? "1 object selected"
            : $"{n} objects selected";
    }
}
