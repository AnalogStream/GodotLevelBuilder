using Godot;
using LevelBuilder.Core;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;
using LevelBuilder.Editor.Camera;
using LevelBuilder.Editor.Commands;
using LevelBuilder.Editor.Grid;
using LevelBuilder.Editor.Session;
using LevelBuilder.Editor.Tools;
using LevelBuilder.Editor.View;

namespace LevelBuilder.App;

/// <summary>
/// Editor shell root. Builds an in-memory document with one storey, wires up the grid,
/// camera, cursor, live view, command stack and tools.
///
/// M2: draw floors (F) and walls (W) on the grid; undo/redo with Ctrl+Z / Ctrl+Y.
/// UI (toolbars/panels) is intentionally last — driven by hotkeys for now.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        GD.Print("=== LevelBuilder — editor shell (M2: floor + wall tools) ===");

        LevelDocument doc = NewDocument(out StoreyData storey);
        PrimitiveRegistry registry = PrimitiveRegistry.CreateDefault();

        var grid = new GridRenderer { CellSize = doc.Grid.CellSize };
        var levelView = new LevelView();
        levelView.Setup(doc, registry);
        var previewLayer = new Node3D { Name = "PreviewLayer" };
        var cursor = new GridCursor { CellSize = doc.Grid.CellSize, Elevation = storey.BaseElevation };
        var cameraRig = new EditorCameraRig();
        var picker = new InstancePicker();
        var tools = new ToolManager();

        AddChild(grid);
        AddChild(levelView);
        AddChild(previewLayer);
        AddChild(cursor);     // before ToolManager so HoveredCell/Corner is fresh each frame
        AddChild(cameraRig);
        AddChild(picker);
        AddChild(tools);
        AddChild(BuildSunLight());
        AddChild(BuildEnvironment());

        var ctx = new EditorContext
        {
            Document = doc,
            Storey = storey,
            Registry = registry,
            Commands = new CommandStack(),
            View = levelView,
            Cursor = cursor,
            PreviewLayer = previewLayer,
            Picker = picker,
        };
        tools.Setup(ctx);
    }

    private static LevelDocument NewDocument(out StoreyData storey)
    {
        storey = new StoreyData { Id = Ids.New(), Name = "Ground Floor", BaseElevation = 0f, Height = 3f };
        var doc = new LevelDocument { Name = "Untitled" };
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
