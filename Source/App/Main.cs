using Godot;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Camera;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Gizmos;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;
using LevelBuilder.Editor.Tools;
using LevelBuilder.Editor.View;
using LevelBuilder.UI;

namespace LevelBuilder.App;

/// <summary>
/// Editor shell root. Builds an in-memory document with one storey, wires up the grid,
/// camera, cursor, live view, command stack and tools.
///
/// Layout: the 3D world lives in its own <see cref="SubViewport"/> so the docked UI can
/// shrink the viewport rather than overlap it. The scene-tree panel docks to its left via
/// an <see cref="HSplitContainer"/>; mouse + keyboard reach the 3D nodes through the
/// container's input forwarding.
///
/// M2: draw floors (F) and walls (W) on the grid; undo/redo with Ctrl+Z / Ctrl+Y.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        GD.Print("=== LevelBuilder — editor shell (docked scene tree) ===");

        // App config + workspace: the writable home for levels and custom textures, chosen by the
        // user (res:// is read-only once the builder is exported as a binary). Restored from
        // user://levelbuilder.cfg; the static Workspace pointer drives the texture path helpers.
        AppConfig config = AppConfig.Load();
        if (config.HasWorkspace) Workspace.SetRoot(config.WorkspacePath);

        LevelDocument doc = NewDocument(out StoreyData storey);
        PrimitiveRegistry registry = PrimitiveRegistry.CreateDefault();

        var grid = new GridRenderer { CellSize = doc.Grid.CellSize };
        var levelView = new LevelView();
        levelView.Setup(doc, registry);
        var previewLayer = new Node3D { Name = "PreviewLayer" };
        var cursor = new GridCursor { CellSize = doc.Grid.CellSize, Elevation = storey.BaseElevation };
        var cameraRig = new EditorCameraRig();
        var picker = new InstancePicker();
        var gizmos = new GizmoLayer { Name = "GizmoLayer" };
        var tools = new ToolManager();

        // The 3D world renders into a SubViewport (its own World3D + physics space), so the
        // docked panel can take screen space without occluding the view.
        var viewport = new SubViewport { RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        viewport.AddChild(grid);
        viewport.AddChild(levelView);
        viewport.AddChild(previewLayer);
        viewport.AddChild(gizmos);
        viewport.AddChild(cursor);     // before ToolManager so HoveredCell/Corner is fresh each frame
        viewport.AddChild(cameraRig);
        viewport.AddChild(picker);
        viewport.AddChild(tools);
        viewport.AddChild(BuildSunLight());
        viewport.AddChild(BuildEnvironment());

        // Stretch makes the SubViewport track the container's size, which is what keeps the
        // mouse-to-camera projection correct even though the viewport is offset by the panel.
        // The container also accepts dropped texture swatches (raycasts to the object under the drop).
        var viewportContainer = new ViewportDropContainer
        {
            Stretch = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        viewportContainer.AddChild(viewport);

        var sceneTree = new SceneTreePanel();
        var inspector = new InspectorPanel();

        // Top row: scene-tree | (3D view | inspector). Nested split so the viewport expands
        // while both side docks keep their width.
        var rightSplit = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rightSplit.AddChild(viewportContainer); // expands to fill
        rightSplit.AddChild(inspector);         // right dock (fixed width)

        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        split.AddChild(sceneTree);  // left dock
        split.AddChild(rightSplit); // viewport + inspector

        // Bottom dock: tabbed — primitive palette + texture library + project actions.
        var palette = new PrimitivePalettePanel { Name = "Primitives" };
        var textures = new TexturePalettePanel { Name = "Textures" };
        var project = new ProjectPanel { Name = "Project" };
        var bottomTabs = new TabContainer { CustomMinimumSize = new Vector2(0, 180) };
        bottomTabs.AddChild(palette);
        bottomTabs.AddChild(textures);
        bottomTabs.AddChild(project);

        var outer = new VSplitContainer();
        outer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        outer.AddChild(split);       // top: viewport row (expands)
        outer.AddChild(bottomTabs);  // bottom: tabbed dock
        AddChild(outer);

        var ctx = new EditorContext
        {
            Registry = registry,
            Commands = new CommandStack(),
            View = levelView,
            Cursor = cursor,
            Grid = grid,
            PreviewLayer = previewLayer,
            Picker = picker,
            Gizmos = gizmos,
            Config = config,
        };
        ctx.ReplaceDocument(doc);    // set the initial document BEFORE panels read ctx.Document in their Setup
        sceneTree.Setup(ctx);        // panels self-populate here and subscribe for later Changed events
        inspector.Setup(ctx);
        viewportContainer.Setup(viewport, ctx.AssignTextureToInstance); // drop a swatch onto an object
        tools.Setup(ctx);
        ctx.CancelActiveTool = tools.CancelActive; // so a document swap cancels a half-drawn primitive (and a height change)

        // Draw-height indicator: a corner Control over the 3D view (not inside the SubViewport). Added
        // AFTER the drop overlay so it stays the topmost child and its scrub drag isn't intercepted.
        var heightIndicator = new HeightIndicatorPanel();
        viewportContainer.AddChild(heightIndicator);
        heightIndicator.Setup(ctx);
        palette.Setup(registry, tools); // after tools.Setup so the primitive->tool map exists
        textures.Setup();
        project.Setup(ctx, config, textures.Refresh); // Change-workspace repopulates the texture palette

        // Resume where we left off: reopen the last saved level if it still exists.
        if (config.HasWorkspace && !string.IsNullOrEmpty(config.LastLevelPath)
            && FileAccess.FileExists(config.LastLevelPath))
            ctx.OpenLevel(config.LastLevelPath);
    }

    private static LevelDocument NewDocument(out StoreyData storey)
    {
        storey = new StoreyData { Id = Ids.New(), Name = "Ground Floor", BaseElevation = 0f, Height = 3f };
        var doc = new LevelDocument { Name = "Untitled" };
        DefaultMaterials.Seed(doc.Materials);
        doc.Storeys.Add(storey);
        return doc;
    }

    private static DirectionalLight3D BuildSunLight()
    {
        var light = new DirectionalLight3D { ShadowEnabled = true };
        light.RotationDegrees = new Vector3(-55, -45, 0);
        return light;
    }

    private static WorldEnvironment BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.16f, 0.17f, 0.19f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.57f, 0.62f),
            AmbientLightEnergy = 0.4f,
        };
        return new WorldEnvironment { Environment = env };
    }
}
