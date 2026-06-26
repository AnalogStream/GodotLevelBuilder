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

    /// <summary>
    /// Closest point on the segment a→b to the ray {from + s·dir, s≥0}. <paramref name="dir"/> must be
    /// unit length. Returns the segment point, its parameter <paramref name="segT"/> ∈ [0,1], and the 3D
    /// distance between the segment and the ray (how close the cursor came to the line). Standard
    /// two-segment closest-point, with the ray clamped to s≥0 and the segment to t∈[0,1].
    /// </summary>
    public static void ClosestRaySegment(Vector3 from, Vector3 dir, Vector3 a, Vector3 b,
        out Vector3 segPoint, out float segT, out float dist)
    {
        Vector3 d2 = b - a;
        Vector3 r = from - a;
        float e = d2.Dot(d2);
        float c = dir.Dot(r);
        if (e < 1e-9f) // degenerate segment: treat as the point a
        {
            segT = 0f;
            segPoint = a;
            dist = (from + dir * Mathf.Max(0f, -c)).DistanceTo(a);
            return;
        }
        float f = d2.Dot(r);
        float bb = dir.Dot(d2);
        float denom = e - bb * bb;            // a·e − b² with a = dir·dir = 1
        float s = Mathf.Abs(denom) > 1e-9f ? Mathf.Max(0f, (bb * f - c * e) / denom) : 0f;
        float t = (bb * s + f) / e;
        if (t < 0f) { t = 0f; s = Mathf.Max(0f, -c); }
        else if (t > 1f) { t = 1f; s = Mathf.Max(0f, bb - c); }
        segT = t;
        segPoint = a + d2 * t;
        dist = segPoint.DistanceTo(from + dir * s);
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
