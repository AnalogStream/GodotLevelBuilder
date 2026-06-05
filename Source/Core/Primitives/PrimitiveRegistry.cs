using System.Collections.Generic;

namespace LevelBuilder.Core.Primitives;

/// <summary>Catalog of available primitive types. The extension point for new primitives.</summary>
public sealed class PrimitiveRegistry
{
    private readonly Dictionary<string, IPrimitive> _byId = new();

    public void Register(IPrimitive primitive) => _byId[primitive.TypeId] = primitive;

    public IPrimitive Get(string typeId) => _byId.TryGetValue(typeId, out IPrimitive p) ? p : null;

    public bool Has(string typeId) => _byId.ContainsKey(typeId);

    public IEnumerable<IPrimitive> All => _byId.Values;

    /// <summary>The built-in catalog.</summary>
    public static PrimitiveRegistry CreateDefault()
    {
        var registry = new PrimitiveRegistry();
        registry.Register(new FloorPrimitive());
        registry.Register(new WallPrimitive());
        registry.Register(new RampPrimitive());
        registry.Register(new StairsPrimitive());
        return registry;
    }
}
