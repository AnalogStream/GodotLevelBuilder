using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Build;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Gizmos;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.View;

namespace LevelBuilder.Editor.Session;

/// <summary>
/// Shared editing state passed to tools: the active document/storey, the primitive
/// registry, the undo stack, the live view, the grid cursor, and a layer to park
/// transient draw previews. The single hub tools talk to.
/// </summary>
public sealed class EditorContext
{
    public LevelDocument Document { get; init; }
    public StoreyData Storey { get; init; }
    public PrimitiveRegistry Registry { get; init; }
    public CommandStack Commands { get; init; }
    public LevelView View { get; init; }
    public GridCursor Cursor { get; init; }
    public Node3D PreviewLayer { get; init; }
    public InstancePicker Picker { get; init; }
    public GizmoLayer Gizmos { get; init; }

    public string SelectedId { get; private set; }
    /// <summary>Non-null when an opening is selected; <see cref="SelectedId"/> is then its owning wall.</summary>
    public string SelectedOpeningId { get; private set; }

    private IReadOnlyList<IEditHandle> _handles = new List<IEditHandle>();
    /// <summary>Resize handles for the current selection (indexed by the picker's HandleIndex).</summary>
    public IReadOnlyList<IEditHandle> Handles => _handles;

    /// <summary>
    /// Re-syncs everything derived from selection + document state: the view's selection, the live
    /// mesh, and the gizmo handles. The single choke point for "the scene changed" — every command,
    /// selection change, and live drag frame routes through here, so the handle widgets track edits
    /// as they happen. (A live drag holds its handle in SelectTool, so rebuilding this list doesn't
    /// disturb the in-flight drag.)
    /// </summary>
    public void Refresh()
    {
        View.SetSelection(SelectedId, SelectedOpeningId);
        _handles = BuildHandles();
        View.Rebuild();
        Gizmos.Rebuild(_handles);
    }

    private List<IEditHandle> BuildHandles()
    {
        if (SelectedOpeningId != null)
        {
            (PrimitiveInstanceData wall, OpeningData opening) = FindOpening(SelectedId, SelectedOpeningId);
            return OpeningHandleProvider.Build(wall, opening, ElevationOffset);
        }
        if (SelectedId == null) return new List<IEditHandle>();
        PrimitiveInstanceData inst = GetInstance(SelectedId);
        if (inst == null) return new List<IEditHandle>();
        return InstanceHandleProvider.Build(inst, Registry.Get(inst.PrimitiveType), ElevationOffset);
    }

    /// <summary>World-space offset of the active storey's floor plane.</summary>
    public Vector3 ElevationOffset => new(0, Storey.BaseElevation, 0);

    public BuildContext BuildCtx() => new()
    {
        Materials = Document.Materials,
        CellSize = Document.Grid.CellSize,
        StoreyHeight = Storey.Height,
    };

    public void AddInstance(PrimitiveInstanceData instance)
        => Commands.Execute(new AddInstanceCommand(Storey, instance, Refresh));

    public void Undo() => Commands.Undo();
    public void Redo() => Commands.Redo();

    // ---- selection -------------------------------------------------------

    /// <summary>Picks the instance (or opening) under the mouse and selects it (or clears on a miss).</summary>
    public void PickAndSelect()
    {
        PickResult r = Picker.Pick();
        if (!r.Hit) ClearSelection();
        else if (r.IsOpening) SelectOpening(r.InstanceId, r.OpeningId);
        else Select(r.InstanceId);
    }

    public PrimitiveInstanceData GetInstance(string id)
    {
        (_, PrimitiveInstanceData inst, _) = Find(id);
        return inst;
    }

    public void AddOpening(PrimitiveInstanceData wall, OpeningData opening)
        => Commands.Execute(new AddOpeningCommand(wall, opening, Refresh));

    public void Select(string id)
    {
        SelectedId = id;
        SelectedOpeningId = null;
        Refresh();
    }

    /// <summary>Selects an opening: the wall is drawn intact and the opening shows as a solid placeholder.</summary>
    public void SelectOpening(string wallId, string openingId)
    {
        SelectedId = wallId;
        SelectedOpeningId = openingId;
        Refresh();
    }

    public void ClearSelection()
    {
        if (SelectedId == null && SelectedOpeningId == null) return;
        SelectedId = null;
        SelectedOpeningId = null;
        Refresh();
    }

    public void DeleteSelected()
    {
        if (SelectedOpeningId != null)
        {
            (PrimitiveInstanceData wall, OpeningData opening) = FindOpening(SelectedId, SelectedOpeningId);
            if (opening == null) { ClearSelection(); return; }

            SelectedId = null;
            SelectedOpeningId = null; // command's refresh will rebuild without the placeholder
            Commands.Execute(new RemoveOpeningCommand(wall, opening, Refresh));
            return;
        }

        if (SelectedId == null) return;
        (StoreyData storey, PrimitiveInstanceData inst, int index) = Find(SelectedId);
        if (inst == null) { ClearSelection(); return; }

        SelectedId = null; // command's refresh will rebuild without the highlight
        Commands.Execute(new RemoveInstanceCommand(storey, inst, index, Refresh));
    }

    private (PrimitiveInstanceData, OpeningData) FindOpening(string wallId, string openingId)
    {
        PrimitiveInstanceData wall = GetInstance(wallId);
        if (wall == null) return (null, null);
        foreach (OpeningData o in wall.Openings)
            if (o.Id == openingId) return (wall, o);
        return (null, null);
    }

    private (StoreyData, PrimitiveInstanceData, int) Find(string id)
    {
        foreach (StoreyData s in Document.Storeys)
        {
            for (int i = 0; i < s.Instances.Count; i++)
                if (s.Instances[i].Id == id) return (s, s.Instances[i], i);
        }
        return (null, null, -1);
    }

    private const string SourceDir = "res://Saved";
    private const string BakedDir = "res://Baked";

    /// <summary>Saves the editable source .tres (re-openable).</summary>
    public void SaveSource()
    {
        EnsureDir(SourceDir);
        string path = $"{SourceDir}/{FileStem()}.tres";
        Error e = LevelSerializer.Save(Document, path);
        Report("save", path, e);
    }

    /// <summary>Bakes a game-ready .tscn (meshes + collision) you can open in Godot.</summary>
    public void BakeToGodot()
    {
        EnsureDir(BakedDir);
        string path = $"{BakedDir}/{FileStem()}.tscn";
        Error e = new SceneBaker(Registry).BakeToFile(Document, path);
        Report("bake", path, e);
    }

    private string FileStem()
    {
        string raw = string.IsNullOrWhiteSpace(Document.Name) ? "Untitled" : Document.Name;
        return raw.Replace(' ', '_');
    }

    private static void EnsureDir(string dir)
    {
        Error e = DirAccess.MakeDirRecursiveAbsolute(dir);
        if (e != Error.Ok && e != Error.AlreadyExists) GD.PushWarning($"Could not create {dir}: {e}");
    }

    private static void Report(string action, string path, Error e)
    {
        if (e == Error.Ok) GD.Print($"[{action}] wrote {path}");
        else GD.PrintErr($"[{action}] failed ({e}) for {path}");
    }
}
