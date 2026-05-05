using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AlmanacCodex.Handbook;

public enum SubmodStatus
{
    Loaded,
    NotInstalled,
    InDevelopment,
}

public record SubmodCard(
    string OwnerModId,
    string NameKey,
    string BlurbKey,
    string GetStartedKey,
    SubmodStatus Status);

public static class SubmodCards
{
    // (mod id, slug, in-development?). When in-development, status reports as InDevelopment regardless of load state.
    private static readonly (string ModId, string Slug, bool InDevelopment)[] Definitions =
    {
        ("almanacforager", "forager", false),
        ("almanacapothecary", "apothecary", true),
        ("almanacalchemist", "alchemist", true),
    };

    public static IReadOnlyList<SubmodCard> Build(IModLoader modLoader)
    {
        var result = new List<SubmodCard>(Definitions.Length);
        foreach (var (modId, slug, inDev) in Definitions)
        {
            SubmodStatus status;
            if (inDev) status = SubmodStatus.InDevelopment;
            else if (modLoader.IsModEnabled(modId)) status = SubmodStatus.Loaded;
            else status = SubmodStatus.NotInstalled;

            result.Add(new SubmodCard(
                OwnerModId: modId,
                NameKey: $"almanaccodex:submod-{slug}-name",
                BlurbKey: $"almanaccodex:submod-{slug}-blurb",
                GetStartedKey: $"almanaccodex:submod-{slug}-getstarted",
                Status: status));
        }
        return result;
    }
}
