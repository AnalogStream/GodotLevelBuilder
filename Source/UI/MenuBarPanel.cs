using Godot;
using LevelBuilder.Editor.Session;

namespace LevelBuilder.UI;

/// <summary>
/// Top menu bar: File / Edit / View / Help. Items are CLICK-ONLY — the keyboard shortcuts shown in
/// the labels are cosmetic, because <see cref="Editor.Tools.ToolManager"/> stays the single owner of
/// every contested key (a real PopupMenu accelerator fires even while a SpinBox/LineEdit has focus,
/// which would e.g. delete the selected object while editing text). The one exception is F1
/// (Hotkey Reference): nothing else owns it and it must work regardless of focus, so it is a real
/// accelerator.
///
/// New/Open/Quit route through Main's confirm-if-dirty guard rather than acting directly.
/// </summary>
public partial class MenuBarPanel : MenuBar
{
    private const int FileNew = 0;
    private const int FileOpen = 1;
    private const int FileSave = 2;
    private const int FileBake = 3;
    private const int FileBakeMerged = 4;
    private const int FileExport = 5;
    private const int FileQuit = 6;

    private const int EditUndo = 0;
    private const int EditRedo = 1;
    private const int EditDelete = 2;

    private const int ViewTopDown = 0;
    private const int HelpHotkeys = 0;

    private EditorContext _ctx;
    private System.Action _requestNew;
    private System.Action _requestOpen;
    private System.Action _requestQuit;
    private System.Action _toggleTopDown;
    private System.Action _toggleHelp;

    public void Setup(EditorContext ctx, System.Action requestNew, System.Action requestOpen,
        System.Action requestQuit, System.Action toggleTopDown, System.Action toggleHelp)
    {
        _ctx = ctx;
        _requestNew = requestNew;
        _requestOpen = requestOpen;
        _requestQuit = requestQuit;
        _toggleTopDown = toggleTopDown;
        _toggleHelp = toggleHelp;

        var file = AddMenu("File");
        file.AddItem("New Level", FileNew);
        file.AddItem("Open Level…", FileOpen);
        file.AddItem("Save                  Ctrl+S", FileSave);
        file.AddSeparator();
        file.AddItem("Bake (per-object)     Ctrl+B", FileBake);
        file.AddItem("Bake Merged Chunk", FileBakeMerged);
        file.AddItem("Export to Game", FileExport);
        file.AddSeparator();
        file.AddItem("Quit", FileQuit);
        file.IdPressed += OnFile;

        var edit = AddMenu("Edit");
        edit.AddItem("Undo                  Ctrl+Z", EditUndo);
        edit.AddItem("Redo                  Ctrl+Y", EditRedo);
        edit.AddSeparator();
        edit.AddItem("Delete Selected       Del", EditDelete);
        edit.IdPressed += OnEdit;

        var view = AddMenu("View");
        view.AddItem("Toggle Top-Down       7", ViewTopDown);
        view.IdPressed += OnView;

        var help = AddMenu("Help");
        help.AddItem("Hotkey Reference", HelpHotkeys, Key.F1); // real accelerator — F1 has no other owner
        help.IdPressed += OnHelp;
    }

    private PopupMenu AddMenu(string title)
    {
        var popup = new PopupMenu { Name = title };
        AddChild(popup);
        return popup;
    }

    private void OnFile(long id)
    {
        switch (id)
        {
            case FileNew: _requestNew?.Invoke(); break;
            case FileOpen: _requestOpen?.Invoke(); break;
            case FileSave: _ctx.SaveSource(); break;
            case FileBake: _ctx.BakeToGodot(); break;
            case FileBakeMerged: _ctx.BakeMergedToGodot(); break;
            case FileExport: _ctx.ExportToGame(); break;
            case FileQuit: _requestQuit?.Invoke(); break;
        }
    }

    private void OnEdit(long id)
    {
        switch (id)
        {
            case EditUndo: _ctx.Undo(); break;
            case EditRedo: _ctx.Redo(); break;
            case EditDelete: _ctx.DeleteSelected(); break;
        }
    }

    private void OnView(long id)
    {
        if (id == ViewTopDown) _toggleTopDown?.Invoke();
    }

    private void OnHelp(long id)
    {
        if (id == HelpHotkeys) _toggleHelp?.Invoke();
    }
}
