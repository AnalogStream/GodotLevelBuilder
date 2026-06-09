using Godot;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Project actions — the "Project" tab of the bottom dock. Buttons for the document-level
/// operations that were previously hotkey-only (save / bake), plus the new merged "chunk" bake.
/// Each button routes through the same <see cref="EditorContext"/> entry point as its hotkey, so
/// behaviour stays identical.
///
/// FocusMode is None on every button so a focused button can't swallow the tool hotkeys
/// (same reason as the primitive palette / scene tree).
/// </summary>
public partial class ProjectPanel : MarginContainer
{
    private EditorContext _ctx;

    public void Setup(EditorContext ctx)
    {
        _ctx = ctx;

        AddThemeConstantOverride("margin_left", 8);
        AddThemeConstantOverride("margin_top", 8);
        AddThemeConstantOverride("margin_right", 8);
        AddThemeConstantOverride("margin_bottom", 8);

        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(rows);

        rows.AddChild(Section("Save"));
        var saveRow = Row(rows);
        saveRow.AddChild(MakeButton("Save Source (.tres)", () => _ctx.SaveSource()));

        rows.AddChild(Section("Export"));
        var bakeRow = Row(rows);
        bakeRow.AddChild(MakeButton("Bake (per-object)", () => _ctx.BakeToGodot()));
        bakeRow.AddChild(MakeButton("Bake Merged Chunk", () => _ctx.BakeMergedToGodot()));

        rows.AddChild(new Label
        {
            Text = "Merged chunk = all geometry combined into one mesh per material + one precise "
                 + "trimesh collision. Fewest draw calls; for assembling maps from chunks. "
                 + "Per-object material overrides collapse to per-material.",
            Modulate = new Color(1, 1, 1, 0.55f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    private static Label Section(string text) =>
        new() { Text = text, Modulate = new Color(1, 1, 1, 0.6f) };

    private static HFlowContainer Row(Node parent)
    {
        var flow = new HFlowContainer();
        parent.AddChild(flow);
        return flow;
    }

    private static Button MakeButton(string text, System.Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.None, // don't let a focused button eat tool hotkeys
            CustomMinimumSize = new Vector2(160, 36),
        };
        button.Pressed += onPressed;
        return button;
    }
}
