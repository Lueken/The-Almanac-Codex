using Vintagestory.API.Common;

namespace AlmanacCodex.Registry;

public class ProcessDefinition
{
    public string Code { get; }
    public string DisplayKey { get; }
    public AssetLocation? IconLocked { get; }
    public AssetLocation? IconUnlocked { get; }
    public string OwnerModId { get; }

    public AssetLocation? OutcomeCode { get; init; }
    public int OutcomeQuantity { get; init; } = 1;
    public string? FlavorKey { get; init; }
    public string? HintKey { get; init; }

    public ProcessDefinition(string code, string displayKey, string ownerModId,
        AssetLocation? iconLocked = null, AssetLocation? iconUnlocked = null)
    {
        Code = code;
        DisplayKey = displayKey;
        OwnerModId = ownerModId;
        IconLocked = iconLocked;
        IconUnlocked = iconUnlocked;
    }
}
