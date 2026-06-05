using System;
using Godot;
using LevelBuilder.Editor.Commands;

namespace LevelBuilder.Editor.Gizmos;

/// <summary>
/// A draggable editing affordance — move, slide along a wall, or resize a dimension. Move/slide/
/// resize all implement this so <c>SelectTool</c> drives them through one generic loop instead of a
/// per-mode switch. Originals are captured at construction and <see cref="Preview"/> applies the
/// change relative to the press point, so it is idempotent and a press-without-drag is a clean no-op.
/// (A future floor-corner edit is just another implementation — a planar, 2-DOF handle.)
/// </summary>
public interface IEditHandle
{
    /// <summary>World position of the grab widget. Ignored for handles grabbed via the body collider.</summary>
    Vector3 Anchor { get; }

    /// <summary>Project the cursor ray onto this handle's constraint (axis line or plane). False if degenerate.</summary>
    bool Grab(Vector3 rayFrom, Vector3 rayDir, out Vector3 world);

    /// <summary>Live-apply the drag from <paramref name="grabStart"/> to <paramref name="grabNow"/>, relative to the originals.</summary>
    void Preview(Vector3 grabStart, Vector3 grabNow);

    /// <summary>Roll back to the original state (drag cancelled).</summary>
    void Cancel();

    /// <summary>True if the current state differs from the original (gates whether a command is pushed).</summary>
    bool Changed { get; }

    /// <summary>The command capturing original → current; the state is already applied by <see cref="Preview"/>.</summary>
    ICommand Commit(Action refresh);
}
