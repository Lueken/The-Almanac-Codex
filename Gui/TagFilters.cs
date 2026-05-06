namespace AlmanacCodex.Gui;

/// <summary>
/// Trait tags applied by Forager (and future sub-mods) to vanilla flora via tagsByType patches.
/// Codes are the live registry strings; slugs are used to build the lang keys.
/// </summary>
internal record TagFilter(string Code, string Slug);

internal static class TagFilters
{
    public static readonly TagFilter[] All =
    {
        new("almanac-aromatic", "aromatic"),
        new("almanac-medicinal", "medicinal"),
        new("almanac-toxic", "toxic"),
        new("almanac-psychoactive", "psychoactive"),
        new("almanac-culinary", "culinary"),
        new("almanac-fibrous", "fibrous"),
        new("almanac-decorative", "decorative"),
        new("almanac-sweet", "sweet"),
        new("almanac-acidic", "acidic"),
        new("almanac-starchy", "starchy"),
        new("almanac-leafy", "leafy"),
        new("almanac-seedy", "seedy"),
        new("almanac-fruity", "fruity"),
    };
}
