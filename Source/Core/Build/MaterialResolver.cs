using System.Collections.Generic;
using Godot;
using LevelBuilder.Core.Data;
using LevelBuilder.Core.Primitives;

namespace LevelBuilder.Core.Build;

/// <summary>
/// Resolves a primitive instance's material slots to real <see cref="Material"/>s and writes
/// them onto the mesh SURFACE (never surface_material_override — that stays free for the
/// consuming game; see docs/EXPORT.md). Shared by the live LevelView and the SceneBaker so
/// the editor preview matches the bake exactly. Loaded materials are cached by id; a missing
/// or unloadable material resolves to null (surface stays default grey, no crash).
/// </summary>
public sealed class MaterialResolver
{
    private readonly Dictionary<string, Material> _cache = new();

    /// <summary>Maps each material slot of <paramref name="prim"/> to a library material on the matching surface.
    /// When <paramref name="embed"/> is true the material is made fully self-contained (pathless textures)
    /// so an exported .tscn carries no res:// dependency — see <see cref="ResolveEmbedded"/>.</summary>
    public void AssignSurfaceMaterials(ArrayMesh mesh, IPrimitive prim, PrimitiveInstanceData inst, MaterialLibrary library, bool embed = false)
    {
        int surfaces = mesh.GetSurfaceCount();
        for (int i = 0; i < prim.MaterialSlots.Count && i < surfaces; i++)
        {
            string slot = prim.MaterialSlots[i];
            if (!inst.MaterialSlots.ContainsKey(slot)) continue;

            string id = inst.MaterialSlots[slot].AsString();
            Material mat = embed ? ResolveEmbedded(id, library) : Resolve(id, library);
            if (mat != null) mesh.SurfaceSetMaterial(i, mat);
        }
    }

    /// <summary>Drops a material's cached build so the next Resolve rebuilds it (call after editing the entry).</summary>
    public void Invalidate(string materialId)
    {
        if (!string.IsNullOrEmpty(materialId)) _cache.Remove(materialId);
    }

    /// <summary>Drops the entire cache (e.g. when a different document, with its own library, is opened).</summary>
    public void Clear()
    {
        _cache.Clear();
        _embedCache.Clear();
    }

    /// <summary>Library id -> Material (loaded from the entry's MaterialPath), cached. Null if unresolved.</summary>
    public Material Resolve(string materialId, MaterialLibrary library)
    {
        if (string.IsNullOrEmpty(materialId)) return null;
        if (_cache.TryGetValue(materialId, out Material cached)) return cached;

        MaterialEntry entry = library?.Find(materialId);
        Material mat = BuildFrom(entry);

        _cache[materialId] = mat;
        return mat;
    }

    /// <summary>
    /// Like <see cref="Resolve"/>, but returns a fully <em>self-contained</em> material: a
    /// StandardMaterial3D whose textures are pathless <see cref="ImageTexture"/>s (decoded from the
    /// image data, no resource_path). A pathless resource serializes <b>inline</b> as a sub_resource,
    /// so an exported .tscn carries its textures with it and works in any project — no res:// path on
    /// either side. Both texture-entry materials and proto .material files are flattened this way.
    /// (Non-StandardMaterial3D materials — e.g. ShaderMaterial — are returned unchanged.)
    /// </summary>
    public Material ResolveEmbedded(string materialId, MaterialLibrary library)
    {
        if (string.IsNullOrEmpty(materialId)) return null;
        if (_embedCache.TryGetValue(materialId, out Material cached)) return cached;

        Material mat = MakeSelfContained(Resolve(materialId, library));
        _embedCache[materialId] = mat;
        return mat;
    }

    private readonly Dictionary<string, Material> _embedCache = new();

    /// <summary>Duplicates a StandardMaterial3D and replaces its (path-bearing) textures with pathless
    /// ImageTextures so the whole thing serializes inline. Leaves other material types as-is.</summary>
    private static Material MakeSelfContained(Material mat)
    {
        if (mat is not StandardMaterial3D std) return mat;

        // Shallow duplicate (subresources: false) so the copy SHARES the original path-bearing
        // textures — then we replace each with a pathless re-wrap. A DEEP duplicate would copy the
        // textures and could strip their resource_path, defeating Embed's path check and leaving a
        // bare CompressedTexture2D that serializes unreliably. Duplicating (not mutating in place)
        // keeps the live-preview cache's original material intact.
        var dup = (StandardMaterial3D)std.Duplicate(false);
        dup.AlbedoTexture = Embed(dup.AlbedoTexture);
        dup.NormalTexture = Embed(dup.NormalTexture); // proto materials are albedo-only today; cover normal too
        return dup;
    }

    /// <summary>A pathless copy of <paramref name="tex"/> (so it embeds inline). Already-pathless textures
    /// pass through; a path-bearing one is re-wrapped from its decoded (decompressed) image.</summary>
    private static Texture2D Embed(Texture2D tex)
    {
        if (tex == null || string.IsNullOrEmpty(tex.ResourcePath)) return tex;
        Image img = tex.GetImage();
        if (img == null) return tex;
        if (img.IsCompressed()) img.Decompress(); // CreateFromImage needs an uncompressed image
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>An entry's material: a loaded .material/.tres if it has one, else a StandardMaterial3D built from its texture.</summary>
    private static Material BuildFrom(MaterialEntry entry)
    {
        if (entry == null) return null;

        if (!string.IsNullOrEmpty(entry.MaterialPath) && ResourceLoader.Exists(entry.MaterialPath))
            return ResourceLoader.Load<Material>(entry.MaterialPath);

        if (!string.IsNullOrEmpty(entry.TexturePath))
        {
            Texture2D tex = TextureLoader.Load(entry.TexturePath); // imported, or raw-decoded if not yet reimported
            if (tex == null) return null;

            float s = entry.UvScale <= 0 ? 1f : entry.UvScale;
            return new StandardMaterial3D
            {
                AlbedoTexture = tex,
                Uv1Scale = new Vector3(s, s, 1), // world-unit UVs → s = tiles per metre
                AlbedoColor = entry.Tint,
            };
        }

        return null;
    }
}
