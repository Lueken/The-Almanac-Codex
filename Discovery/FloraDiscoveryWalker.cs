using System.Collections.Generic;
using AlmanacCodex.Registry;
using Vintagestory.API.Common;

namespace AlmanacCodex.Discovery;

/// <summary>
/// Walks the loaded collectible registry at AssetsFinalize and registers every
/// flora item tagged with one of the <c>almanac-*</c> trait tags as a Codex
/// entry. Metadata (latin name, classification, slug → habitat / description
/// lang keys) comes from <c>assets/almanaccodex/config/codex-entries.json</c>.
///
/// Migrated from Forager's <c>CodexIntegration.RegisterEntriesAndProcesses</c>
/// in Codex v0.2.1. The walker is now part of the Codex platform so flora
/// discovery works even with only Codex installed (previously Forager owned
/// the walk, which meant Codex without Forager registered nothing).
/// </summary>
public static class FloraDiscoveryWalker
{
    public const string OwnerModId = "almanaccodex";

    /// <summary>
    /// Trait-tag codes Codex recognises as flora discovery anchors. Items with
    /// any of these tags get registered. Forager (and future Almanac sub-mods)
    /// patch these tags onto vanilla collectibles via <c>tagsByType</c> JSON.
    /// </summary>
    public static readonly string[] AlmanacTagCodes =
    {
        "almanac-aromatic", "almanac-medicinal", "almanac-decorative", "almanac-toxic",
        "almanac-culinary", "almanac-psychoactive", "almanac-fibrous",
        "almanac-fruity", "almanac-sweet", "almanac-acidic",
        "almanac-starchy", "almanac-leafy", "almanac-seedy",
    };

    public static void Run(ICoreAPI api)
    {
        var defs = LoadEntryDefs(api);

        var registry = api.CollectibleTagRegistry;
        var err = registry.TryCreateTagSetAndLogIssues(out var almanacTags, AlmanacTagCodes);
        CodexLogger.Info(api, "flora-walker",
            $"built lookup TagSet for {AlmanacTagCodes.Length} known almanac-* tags (result={err})");

        int registered = 0;
        int withMeta = 0;
        foreach (var collectible in api.World.Collectibles)
        {
            if (collectible?.Code == null) continue;
            if (collectible.Tags.IsEmpty) continue;
            if (!collectible.Tags.Overlaps(almanacTags)) continue;

            var meta = LookupMeta(defs, collectible.Code.Path);
            if (meta != null) withMeta++;

            string? classKey = meta != null && !string.IsNullOrEmpty(meta.Class)
                ? $"almanaccodex:codex-class.{meta.Class}"
                : null;
            string? habitatKey = meta != null && !string.IsNullOrEmpty(meta.Slug)
                ? $"almanaccodex:codex-habitat.{meta.Slug}"
                : null;
            string? descKey = meta != null && !string.IsNullOrEmpty(meta.Slug)
                ? $"almanaccodex:codex-description.{meta.Slug}"
                : null;

            CodexAPI.RegisterEntry(api, new AlmanacEntry(collectible.Code, OwnerModId)
            {
                LatinName = !string.IsNullOrEmpty(meta?.Latin) ? meta.Latin : null,
                ClassificationKey = classKey,
                HabitatKey = habitatKey,
                DescriptionKey = descKey,
            });
            registered++;
        }

        CodexLogger.Info(api, "flora-walker",
            $"registered {registered} flora collectibles ({withMeta} with metadata)");
    }

    // ── Entry-def loader (ported from Forager's CodexEntryLoader) ───────────

    public sealed class EntryDef
    {
        public string Latin { get; set; } = "";
        public string Class { get; set; } = "";
        public string Slug { get; set; } = "";
    }

    private static Dictionary<string, EntryDef> LoadEntryDefs(ICoreAPI api)
    {
        var asset = api.Assets.TryGet(new AssetLocation(OwnerModId, "config/codex-entries.json"));
        if (asset == null)
        {
            CodexLogger.Warn(api, "flora-walker",
                "no config/codex-entries.json found; entries will register without metadata");
            return new Dictionary<string, EntryDef>();
        }

        try
        {
            var data = asset.ToObject<Dictionary<string, EntryDef>>();
            CodexLogger.Info(api, "flora-walker",
                $"loaded {data?.Count ?? 0} entry definitions from config/codex-entries.json");
            return data ?? new Dictionary<string, EntryDef>();
        }
        catch (System.Exception ex)
        {
            CodexLogger.Error(api, "flora-walker",
                $"failed to parse config/codex-entries.json: {ex.GetType().Name}: {ex.Message}");
            return new Dictionary<string, EntryDef>();
        }
    }

    /// <summary>
    /// Resolves a collectible's path to the matching <see cref="EntryDef"/>:
    /// 1) exact-match the full path; 2) progressive prefix shortening (handles
    /// orientation/state suffixes); 3) pattern matching (handles state-in-middle
    /// codes like <c>fruitingbush-*-blackberry</c>). Returns null when no match.
    /// </summary>
    private static EntryDef? LookupMeta(Dictionary<string, EntryDef> defs, string codePath)
    {
        if (defs.TryGetValue(codePath, out var m)) return m;

        var parts = codePath.Split('-');
        for (int i = parts.Length - 1; i >= 1; i--)
        {
            var prefix = string.Join("-", parts, 0, i);
            if (defs.TryGetValue(prefix, out m)) return m;
        }

        foreach (var kvp in defs)
        {
            if (kvp.Key.Contains('*') && MatchesPattern(kvp.Key, codePath)) return kvp.Value;
        }

        return null;
    }

    private static bool MatchesPattern(string pattern, string path)
    {
        var pParts = pattern.Split('-');
        var aParts = path.Split('-');
        if (aParts.Length < pParts.Length) return false;
        for (int i = 0; i < pParts.Length; i++)
        {
            if (pParts[i] == "*") continue;
            if (pParts[i] != aParts[i]) return false;
        }
        return true;
    }
}
