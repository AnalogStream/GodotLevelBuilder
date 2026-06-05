using System;

namespace LevelBuilder.Core;

/// <summary>Stable, unique IDs for storeys / instances / openings.</summary>
public static class Ids
{
    public static string New() => Guid.NewGuid().ToString("N").Substring(0, 8);
}
