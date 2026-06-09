using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Project actions — the "Project" tab of the bottom dock. Document-level operations: choosing the
/// workspace folder, New/Open/Save of the editable level, and baking/export. Each button routes
/// through the same <see cref="EditorContext"/>/<see cref="AppConfig"/> path as the hotkeys.
///
/// FocusMode is None on every button so a focused button can't swallow the tool hotkeys (same reason
/// as the primitive palette / scene tree). The level-name field is a deliberate exception — it needs
/// focus to type, and while focused it harmlessly captures the hotkeys.
/// </summary>
public partial class ProjectPanel : MarginContainer
{
    private EditorContext _ctx;
    private AppConfig _config;
    private System.Action _onWorkspaceChanged;

    private Label _workspaceLabel;
    private Label _targetLabel;
    private LineEdit _nameEdit;
    private Button _exportButton;
    private FileDialog _workspaceDialog;
    private FileDialog _openDialog;
    private FileDialog _targetDialog;

    public void Setup(EditorContext ctx, AppConfig config, System.Action onWorkspaceChanged)
    {
        _ctx = ctx;
        _config = config;
        _onWorkspaceChanged = onWorkspaceChanged;

        AddThemeConstantOverride("margin_left", 8);
        AddThemeConstantOverride("margin_top", 8);
        AddThemeConstantOverride("margin_right", 8);
        AddThemeConstantOverride("margin_bottom", 8);

        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(rows);

        // --- Workspace ---
        rows.AddChild(Section("Workspace"));
        var wsRow = Row(rows);
        _workspaceLabel = new Label
        {
            Modulate = new Color(1, 1, 1, 0.75f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        wsRow.AddChild(_workspaceLabel);
        wsRow.AddChild(MakeButton("Change…", OpenWorkspaceDialog));
        UpdateWorkspaceLabel();

        // --- Level (New / Open / Save) ---
        rows.AddChild(Section("Level"));
        var levelRow = Row(rows);
        _nameEdit = new LineEdit
        {
            PlaceholderText = "Level name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(140, 0),
            Text = _ctx.Document.Name,
        };
        _nameEdit.TextSubmitted += OnNameSubmitted;
        levelRow.AddChild(_nameEdit);
        levelRow.AddChild(MakeButton("New", () => _ctx.NewLevel()));
        levelRow.AddChild(MakeButton("Open…", OpenLevelDialog));
        levelRow.AddChild(MakeButton("Save", SaveLevel));

        // --- Local bake (preview, inside this project) ---
        rows.AddChild(Section("Bake (local preview)"));
        var bakeRow = Row(rows);
        bakeRow.AddChild(MakeButton("Bake (per-object)", () => _ctx.BakeToGodot()));
        bakeRow.AddChild(MakeButton("Bake Merged Chunk", () => _ctx.BakeMergedToGodot()));

        // --- Export to the target game project ---
        rows.AddChild(Section("Export to game"));
        var targetRow = Row(rows);
        _targetLabel = new Label
        {
            Modulate = new Color(1, 1, 1, 0.75f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        targetRow.AddChild(_targetLabel);
        targetRow.AddChild(MakeButton("Set…", OpenTargetDialog));
        _exportButton = MakeButton("Export to Game", () => _ctx.ExportToGame());
        targetRow.AddChild(_exportButton);
        UpdateTargetLabel();

        rows.AddChild(new Label
        {
            Text = "Export = merged chunk written into the target project's levels/ folder, textures "
                 + "embedded inline (self-contained, no res:// setup). Merged = one mesh per material "
                 + "+ one precise trimesh collision; per-object material overrides collapse to per-material.",
            Modulate = new Color(1, 1, 1, 0.55f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        _ctx.Changed += OnContextChanged; // keep the name field in sync after New/Open
    }

    public override void _ExitTree()
    {
        if (_ctx != null) _ctx.Changed -= OnContextChanged;
    }

    // ---- level name / save ----------------------------------------------

    private void OnNameSubmitted(string text)
    {
        string name = text.Trim();
        if (name.Length > 0) _ctx.Document.Name = name; // document metadata, not undo-tracked geometry
        _nameEdit.ReleaseFocus();
    }

    private void SaveLevel()
    {
        string name = _nameEdit.Text.Trim();
        if (name.Length > 0) _ctx.Document.Name = name; // commit a typed-but-not-submitted name
        _ctx.SaveSource();
    }

    private void OnContextChanged()
    {
        // Reflect the open document's name, but don't clobber what the user is typing.
        if (!_nameEdit.HasFocus() && _nameEdit.Text != _ctx.Document.Name)
            _nameEdit.Text = _ctx.Document.Name;
    }

    private void OpenLevelDialog()
    {
        if (_openDialog == null)
        {
            _openDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Title = "Open level",
                UseNativeDialog = true,
            };
            _openDialog.AddFilter("*.tres", "Level source");
            _openDialog.FileSelected += path => _ctx.OpenLevel(path);
            AddChild(_openDialog);
        }
        if (Workspace.IsSet) _openDialog.CurrentDir = Workspace.LevelsDir;
        _openDialog.PopupCentered(new Vector2I(900, 600));
    }

    // ---- target game project --------------------------------------------

    private void OpenTargetDialog()
    {
        if (_targetDialog == null)
        {
            _targetDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenDir,
                Title = "Choose the target Godot game project folder",
                UseNativeDialog = true,
            };
            _targetDialog.DirSelected += OnTargetChosen;
            AddChild(_targetDialog);
        }
        _targetDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnTargetChosen(string dir)
    {
        _config.TargetProjectPath = dir;
        _config.Save();
        UpdateTargetLabel();
    }

    private void UpdateTargetLabel()
    {
        _targetLabel.Text = _config.HasTarget ? _config.TargetProjectPath : "(no target project set)";
        _exportButton.Disabled = !_config.HasTarget;
    }

    // ---- workspace -------------------------------------------------------

    private void OpenWorkspaceDialog()
    {
        if (_workspaceDialog == null)
        {
            _workspaceDialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenDir,
                Title = "Choose a workspace folder",
                UseNativeDialog = true,
            };
            _workspaceDialog.DirSelected += OnWorkspaceChosen;
            AddChild(_workspaceDialog);
        }
        _workspaceDialog.PopupCentered(new Vector2I(900, 600));
    }

    private void OnWorkspaceChosen(string dir)
    {
        _config.WorkspacePath = dir;
        _config.Save();
        Workspace.SetRoot(dir);
        UpdateWorkspaceLabel();
        _onWorkspaceChanged?.Invoke(); // repopulate the texture palette etc.
    }

    private void UpdateWorkspaceLabel() =>
        _workspaceLabel.Text = _config.HasWorkspace ? _config.WorkspacePath : "(not set — pick a folder)";

    // ---- helpers ---------------------------------------------------------

    private static Label Section(string text) =>
        new() { Text = text, Modulate = new Color(1, 1, 1, 0.6f) };

    private static HFlowContainer Row(Node parent)
    {
        var flow = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(flow);
        return flow;
    }

    private static Button MakeButton(string text, System.Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = FocusModeEnum.None, // don't let a focused button eat tool hotkeys
            CustomMinimumSize = new Vector2(96, 36),
        };
        button.Pressed += onPressed;
        return button;
    }
}
