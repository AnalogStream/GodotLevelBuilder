using Godot;
using LevelBuilder.Editor.Camera;
using LevelBuilder.Editor.Grid;

namespace LevelBuilder.App;

/// <summary>
/// Editor shell root. (Milestone 1's round-trip/bake demo lived here; that pipeline
/// is verified and its code remains in Source/Core — this is now the real app entry.)
///
/// M2.1: reference grid. M2.2: orbit/pan/zoom camera rig.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        GD.Print("=== LevelBuilder — editor shell (M2.3: grid + camera + cursor) ===");

        AddChild(new GridRenderer());
        AddChild(new EditorCameraRig());
        AddChild(new GridCursor());
    }
}
