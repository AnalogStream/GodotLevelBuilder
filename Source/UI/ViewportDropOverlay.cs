using System;
using Godot;

namespace LevelBuilder.UI;

/// <summary>
/// A transparent full-rect drop catcher laid over the 3D view. It exists because a bare
/// SubViewportContainer often doesn't get drag-drop queries (the drag is forwarded into the
/// SubViewport, whose empty GUI swallows it). MouseFilter=Pass lets ordinary mouse input fall
/// through to the container (so orbit/draw still work) while this control still answers
/// _CanDropData/_DropData. On a drop it raycasts the subviewport camera at the drop position
/// (container-local == subviewport pixels at stretch 1:1) to find the object underneath.
/// </summary>
public partial class ViewportDropOverlay : Control
{
    private const uint BodyMask = 1; // instance + opening pick colliders (LevelView default layer)
    private const float RayLength = 2000f;

    private SubViewport _viewport;
    private Action<string, string> _onDropOnInstance; // (instanceId, texturePath)

    // A drop arrives during GUI processing, where World3D.DirectSpaceState is null — so we record the
    // drop and resolve the raycast on the next physics frame (the only place space queries are valid).
    private bool _pending;
    private Vector2 _pendingPosition;
    private string _pendingTexture;

    public void Setup(SubViewport viewport, Action<string, string> onDropOnInstance)
    {
        _viewport = viewport;
        _onDropOnInstance = onDropOnInstance;
        MouseFilter = MouseFilterEnum.Pass; // catch drops, let clicks/drags reach the viewport below
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetPhysicsProcess(true); // ensure the deferred drop resolves even though we created this node in code
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => TextureDrag.TryParse(data, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TextureDrag.TryParse(data, out string texturePath)) return;
        _pendingPosition = atPosition;
        _pendingTexture = texturePath;
        _pending = true; // resolved in _PhysicsProcess, where the space query is valid
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_pending) return;
        _pending = false;

        // The drop position is overlay-local; rescale to subviewport pixels in case the rects differ.
        Vector2 vpPos = _pendingPosition;
        if (Size.X > 0 && Size.Y > 0)
            vpPos = _pendingPosition * ((Vector2)_viewport.Size / Size);

        string instanceId = PickInstanceAt(vpPos);
        if (instanceId != null) _onDropOnInstance?.Invoke(instanceId, _pendingTexture);
    }

    private string PickInstanceAt(Vector2 position)
    {
        Camera3D cam = _viewport?.GetCamera3D();
        // FindWorld3D (not .World3D): the SubViewport has OwnWorld3D=false, so the 3D bodies live in the
        // inherited world. .World3D would hand back the subviewport's own empty world and the ray hits nothing.
        PhysicsDirectSpaceState3D space = _viewport?.FindWorld3D()?.DirectSpaceState;
        if (cam == null || space == null) return null;

        Vector3 from = cam.ProjectRayOrigin(position);
        Vector3 to = from + cam.ProjectRayNormal(position) * RayLength;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = BodyMask;
        Godot.Collections.Dictionary hit = space.IntersectRay(query);
        if (hit.Count == 0) return null;

        var collider = hit["collider"].As<Node>();
        return collider != null && collider.HasMeta("instanceId") ? collider.GetMeta("instanceId").AsString() : null;
    }
}
