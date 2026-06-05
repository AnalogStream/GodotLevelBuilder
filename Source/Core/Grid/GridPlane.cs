using Godot;

namespace LevelBuilder.Core.Grid;

/// <summary>Math for projecting onto the horizontal editing plane.</summary>
public static class GridPlane
{
    /// <summary>
    /// Intersects a ray with the horizontal plane y = <paramref name="planeY"/>.
    /// Returns false if the ray is parallel to the plane or points away from it.
    /// </summary>
    public static bool RayToPlane(Vector3 origin, Vector3 dir, float planeY, out Vector3 hit)
    {
        hit = default;
        if (Mathf.Abs(dir.Y) < 1e-6f) return false;

        float t = (planeY - origin.Y) / dir.Y;
        if (t < 0f) return false;

        hit = origin + dir * t;
        return true;
    }
}
