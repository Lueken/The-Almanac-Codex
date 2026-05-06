using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AlmanacCodex.Gui;

/// <summary>
/// Resolves an item or block to a GL texture handle + UV rect within VS's existing texture
/// atlas. Used to feed ImGui.Image() so the Almanac grid shows real item icons.
///
/// Limitations: resolves to flat sprite textures only. 3D blocks (cube, custom shape) will
/// resolve to one of their face textures. For flora — Forager's main content — this is fine
/// because most plants are flat sprites.
/// </summary>
public readonly struct AtlasIcon
{
    public readonly int TextureId;
    public readonly float U0, V0, U1, V1;

    public AtlasIcon(int textureId, float u0, float v0, float u1, float v1)
    {
        TextureId = textureId; U0 = u0; V0 = v0; U1 = u1; V1 = v1;
    }
}

public static class ItemIconResolver
{
    public static AtlasIcon? Resolve(ICoreClientAPI capi, CollectibleObject? collectible)
    {
        if (collectible == null) return null;

        TextureAtlasPosition? pos = null;

        if (collectible is Item item)
        {
            pos = capi.ItemTextureAtlas.GetPosition(item, returnNullWhenMissing: true);
        }
        else if (collectible is Block block)
        {
            // Try common single-face flora textures first, then walk the textures dict.
            pos = capi.BlockTextureAtlas.GetPosition(block, "all", returnNullWhenMissing: true)
                ?? capi.BlockTextureAtlas.GetPosition(block, "front", returnNullWhenMissing: true)
                ?? capi.BlockTextureAtlas.GetPosition(block, "side", returnNullWhenMissing: true)
                ?? capi.BlockTextureAtlas.GetPosition(block, "up", returnNullWhenMissing: true);

            if (pos == null && block.Textures != null)
            {
                foreach (var kv in block.Textures)
                {
                    pos = capi.BlockTextureAtlas.GetPosition(block, kv.Key, returnNullWhenMissing: true);
                    if (pos != null) break;
                }
            }
        }

        if (pos == null) return null;
        return new AtlasIcon(pos.atlasTextureId, pos.x1, pos.y1, pos.x2, pos.y2);
    }
}
