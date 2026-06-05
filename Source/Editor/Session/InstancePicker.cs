using Godot;

namespace LevelBuilder.Editor.Session;

/// <summary>Outcome of a viewport pick: which instance, and where it was hit.</summary>
public readonly struct PickResult
{
    public bool Hit { get; }
    public string InstanceId { get; }
    /// <summary>Set when an opening's pick box was hit; <see cref="InstanceId"/> is then its owning wall.</summary>
    public string OpeningId { get; }
    public Vector3 Position { get; }

    public PickResult(string instanceId, Vector3 position, string openingId = null)
    {
        Hit = true;
        InstanceId = instanceId;
        OpeningId = openingId;
        Position = position;
    }

    public bool IsOpening => !string.IsNullOrEmpty(OpeningId);
}

/// <summary>
/// Resolves which primitive instance is under the mouse by raycasting from the active
/// camera against the per-instance pick colliders the LevelView builds. Each pick body
/// carries an "instanceId" metadata. The raycast runs in _PhysicsProcess (where physics
/// queries are valid) and the latest result is cached, so callers can read it any time.
/// </summary>
public partial class InstancePicker : Node3D
{
    private const float RayLength = 2000f;
    private PickResult _latest;

    /// <summary>The most recent pick (from the last physics frame).</summary>
    public PickResult Pick() => _latest;

    public override void _PhysicsProcess(double delta) => _latest = Raycast();

    private PickResult Raycast()
    {
        Camera3D cam = GetViewport().GetCamera3D();
        if (cam == null) return default;

        Vector2 mouse = GetViewport().GetMousePosition();
        Vector3 from = cam.ProjectRayOrigin(mouse);
        Vector3 to = from + cam.ProjectRayNormal(mouse) * RayLength;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return default;

        var collider = hit["collider"].As<Node>();
        if (collider == null || !collider.HasMeta("instanceId")) return default;

        string openingId = collider.HasMeta("openingId") ? collider.GetMeta("openingId").AsString() : null;
        return new PickResult(collider.GetMeta("instanceId").AsString(), hit["position"].AsVector3(), openingId);
    }
}
