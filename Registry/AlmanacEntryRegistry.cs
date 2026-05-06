using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AlmanacCodex.Registry;

public class AlmanacEntryRegistry
{
    private readonly Dictionary<string, AlmanacEntry> entries = new();
    private readonly ICoreAPI api;

    public AlmanacEntryRegistry(ICoreAPI api)
    {
        this.api = api;
    }

    public void Register(AlmanacEntry entry)
    {
        var key = entry.Code.ToShortString();
        if (entries.ContainsKey(key)) return;
        entries[key] = entry;
        CodexLogger.Debug(api, "entry-registry",
            $"registered entry '{key}' (owner='{entry.OwnerModId}')");
    }

    public bool IsRegistered(AssetLocation code) => entries.ContainsKey(code.ToShortString());

    public AlmanacEntry? Get(AssetLocation code)
        => entries.TryGetValue(code.ToShortString(), out var e) ? e : null;

    public IReadOnlyCollection<AlmanacEntry> All => entries.Values;

    public int Count => entries.Count;

    public IEnumerable<AlmanacEntry> GetVariantsOfGroup(string groupKey)
    {
        foreach (var entry in entries.Values)
        {
            if (AlmanacEntry.GetGroupKey(entry.Code) == groupKey)
                yield return entry;
        }
    }
}
