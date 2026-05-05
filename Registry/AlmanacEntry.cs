using Vintagestory.API.Common;

namespace AlmanacCodex.Registry;

public class AlmanacEntry
{
    public AssetLocation Code { get; }
    public string OwnerModId { get; }

    public AlmanacEntry(AssetLocation code, string ownerModId)
    {
        Code = code;
        OwnerModId = ownerModId;
    }
}
