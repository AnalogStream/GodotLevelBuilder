using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;

namespace LevelBuilder.Core.Primitives;

/// <summary>
/// A parametric building block. One implementation per primitive type, registered
/// in the PrimitiveRegistry. BuildMesh/BuildCollision must be pure + deterministic
/// (same input -> same output): they run for both live preview and bake.
/// </summary>
public interface IPrimitive
{
    string TypeId { get; }
    string DisplayName { get; }
    /// <summary>Palette grouping, e.g. "Structure", "Trim", "Vertical".</summary>
    string Category { get; }

    /// <summary>Parameter schema (drives the inspector + defaults).</summary>
    IReadOnlyList<ParamSpec> Parameters { get; }

    /// <summary>Named surfaces. Order is stable and maps surface i &lt;-&gt; MaterialSlots[i].</summary>
    IReadOnlyList<string> MaterialSlots { get; }

    /// <summary>Multi-surface mesh in local space (one surface per material slot). CCW, normals + tangents.</summary>
    ArrayMesh BuildMesh(PrimitiveInstanceData data, BuildContext ctx);

    /// <summary>Collision shapes for the baked StaticBody3D (trimesh for static level geometry).</summary>
    Shape3D[] BuildCollision(PrimitiveInstanceData data, BuildContext ctx);
}
