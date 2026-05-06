using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AlmanacCodex.Registry;

public class AlmanacEntry
{
    public AssetLocation Code { get; }
    public string OwnerModId { get; }

    public string? LatinName { get; init; }
    public string? ClassificationKey { get; init; }
    public string? HabitatKey { get; init; }
    public string? DescriptionKey { get; init; }

    public AlmanacEntry(AssetLocation code, string ownerModId)
    {
        Code = code;
        OwnerModId = ownerModId;
    }

    private static readonly HashSet<string> OrientationSuffixes = new(StringComparer.Ordinal)
    {
        "north", "south", "east", "west", "up", "down", "horizontal", "vertical",
    };

    public static string GetGroupKey(AssetLocation code)
    {
        var path = code.Path;
        var parts = path.Split('-');
        if (parts.Length > 1 && OrientationSuffixes.Contains(parts[^1]))
        {
            path = string.Join("-", parts, 0, parts.Length - 1);
        }
        return code.Domain + ":" + path;
    }

    public static bool IsOrientationSuffix(string suffix) => OrientationSuffixes.Contains(suffix);

    public static string? GetOrientationSuffix(AssetLocation code)
    {
        var parts = code.Path.Split('-');
        if (parts.Length > 1 && OrientationSuffixes.Contains(parts[^1]))
            return parts[^1];
        return null;
    }
}
