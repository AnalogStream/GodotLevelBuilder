using Godot;

namespace LevelBuilder.UI;

/// <summary>Shared encoding for a texture drag payload, so swatch sources and drop targets agree.</summary>
public static class TextureDrag
{
    private const string Kind = "texture";

    public static Godot.Collections.Dictionary Payload(string texturePath)
        => new() { { "kind", Kind }, { "path", texturePath } };

    /// <summary>True if <paramref name="data"/> is a texture drag; yields its res:// path.</summary>
    public static bool TryParse(Variant data, out string texturePath)
    {
        texturePath = null;
        if (data.VariantType != Variant.Type.Dictionary) return false;

        Godot.Collections.Dictionary d = data.AsGodotDictionary();
        if (!d.TryGetValue("kind", out Variant kind) || kind.AsString() != Kind) return false;
        if (!d.TryGetValue("path", out Variant path)) return false;

        texturePath = path.AsString();
        return !string.IsNullOrEmpty(texturePath);
    }
}
