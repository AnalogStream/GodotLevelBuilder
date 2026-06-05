using Godot;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>Ray-projection helpers shared by drag handles.</summary>
public static class GizmoMath
{
    /// <summary>
    /// Closest point on the line {anchor + s·axis} to the ray {from + t·dir}. Both
    /// <paramref name="axis"/> and <paramref name="dir"/> must be unit length. False if
    /// the ray is parallel to the axis (no stable closest point).
    /// </summary>
    public static bool ClosestOnAxis(Vector3 anchor, Vector3 axis, Vector3 from, Vector3 dir, out Vector3 point)
    {
        Vector3 w0 = anchor - from;
        float b = axis.Dot(dir);
        float d = axis.Dot(w0);
        float e = dir.Dot(w0);
        float denom = 1f - b * b; // a = c = 1 for unit vectors
        if (Mathf.Abs(denom) < 1e-6f) { point = anchor; return false; }
        float s = (b * e - d) / denom;
        point = anchor + axis * s;
        return true;
    }

    /// <summary>Intersection of the ray {from + t·dir} with the plane through
    /// <paramref name="planePoint"/> with the given <paramref name="planeNormal"/>. False if
    /// parallel or behind the camera.</summary>
    public static bool RayPlane(Vector3 from, Vector3 dir, Vector3 planePoint, Vector3 planeNormal, out Vector3 point)
    {
        point = Vector3.Zero;
        float denom = dir.Dot(planeNormal);
        if (Mathf.Abs(denom) < 1e-6f) return false;
        float t = (planePoint - from).Dot(planeNormal) / denom;
        if (t < 0f) return false;
        point = from + dir * t;
        return true;
    }
}
