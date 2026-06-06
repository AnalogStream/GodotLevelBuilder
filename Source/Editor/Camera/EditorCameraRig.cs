using Godot;

namespace LevelBuilder.Editor.Camera;

/// <summary>
/// Turntable viewport camera, like Godot's / Blender's 3D view:
///   • Middle mouse drag        → orbit around the focus point
///   • Shift + middle mouse drag → pan the focus point
///   • Mouse wheel              → zoom (dolly toward/away from focus)
///   • Press 7                  → toggle orthographic top-down view (Blender numpad-7),
///                                 looking straight down for laying out the floor plan
///
/// This node IS the focus pivot: its Position is the look-at target, its rotation
/// is the orbit, and the child Camera3D sits back along local +Z at <see cref="Distance"/>.
/// </summary>
public partial class EditorCameraRig : Node3D
{
    [Export] public float Distance { get; set; } = 18f;
    [Export] public float MinDistance { get; set; } = 1f;
    [Export] public float MaxDistance { get; set; } = 500f;
    [Export] public float OrbitSensitivity { get; set; } = 0.01f;
    [Export] public float PanSensitivity { get; set; } = 0.0015f;
    [Export] public float ZoomStep { get; set; } = 0.1f;

    private float _yaw = Mathf.DegToRad(-45f);
    private float _pitch = Mathf.DegToRad(-35f);
    private Camera3D _camera;
    private bool _orbiting;
    private bool _panning;
    private bool _topDown;

    // Saved perspective orbit so toggling 7 off restores the previous viewpoint.
    private float _savedYaw;
    private float _savedPitch;

    public override void _Ready()
    {
        _camera = new Camera3D { Name = "Camera3D" };
        AddChild(_camera);
        _camera.Current = true;
        Apply();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Key7 or Key.Kp7 }:
                ToggleTopDown();
                break;
            case InputEventMouseButton mb:
                HandleButton(mb);
                break;
            case InputEventMouseMotion mm when _orbiting || _panning:
                HandleMotion(mm);
                break;
        }
    }

    private void ToggleTopDown()
    {
        if (!_topDown)
        {
            _savedYaw = _yaw;
            _savedPitch = _pitch;
            _yaw = 0f;
            _pitch = Mathf.DegToRad(-90f); // look straight down
            _topDown = true;
        }
        else
        {
            _yaw = _savedYaw;
            _pitch = _savedPitch;
            _topDown = false;
        }
        Apply();
    }

    private void HandleButton(InputEventMouseButton mb)
    {
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp when mb.Pressed:
                Zoom(-1);
                break;
            case MouseButton.WheelDown when mb.Pressed:
                Zoom(1);
                break;
            case MouseButton.Middle:
                _panning = mb.Pressed && mb.ShiftPressed;
                _orbiting = mb.Pressed && !mb.ShiftPressed;
                break;
        }
    }

    private void HandleMotion(InputEventMouseMotion mm)
    {
        if (_orbiting && !_topDown)
        {
            _yaw -= mm.Relative.X * OrbitSensitivity;
            _pitch -= mm.Relative.Y * OrbitSensitivity;
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
            Apply();
        }
        else if (_panning)
        {
            // Pan in the camera's screen plane; scale by distance so it feels constant on screen.
            Basis camBasis = _camera.GlobalTransform.Basis;
            float scale = PanSensitivity * Distance;
            Position += (-camBasis.X * mm.Relative.X + camBasis.Y * mm.Relative.Y) * scale;
        }
    }

    private void Zoom(int dir)
    {
        float factor = 1f + dir * ZoomStep; // in (dir -1) → 0.9, out (dir +1) → 1.1
        Distance = Mathf.Clamp(Distance * factor, MinDistance, MaxDistance);
        Apply();
    }

    private void Apply()
    {
        // Default Euler order (YXZ) gives turntable orbit: yaw about global Y, pitch about local X.
        Rotation = new Vector3(_pitch, _yaw, 0);
        _camera.Position = new Vector3(0, 0, Distance);

        // Top-down uses an orthographic projection (no perspective foreshortening), so the
        // floor plan reads true-to-scale like a blueprint. Size tracks Distance so the wheel
        // still zooms. Perspective everywhere else.
        if (_topDown)
        {
            _camera.Projection = Camera3D.ProjectionType.Orthogonal;
            _camera.Size = Distance;
        }
        else
        {
            _camera.Projection = Camera3D.ProjectionType.Perspective;
        }
    }
}
