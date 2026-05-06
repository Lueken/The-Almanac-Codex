using System.Collections.Generic;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Common;

namespace AlmanacCodex;

/// <summary>
/// Public surface that downstream Almanac sub-mods compile against.
/// Resolved through ModLoader; safe to call from either side. Record* methods
/// route to the server when invoked client-side.
/// </summary>
public static class CodexAPI
{
    private static AlmanacCodexModSystem? Instance(ICoreAPI api)
        => api.ModLoader.GetModSystem<AlmanacCodexModSystem>();

    public static void RegisterEntry(ICoreAPI api, AlmanacEntry entry)
        => Instance(api)?.Entries.Register(entry);

    public static void RegisterProcess(ICoreAPI api, ProcessDefinition def)
        => Instance(api)?.Processes.Register(def);

    public static IReadOnlyCollection<ProcessDefinition> AllProcesses(ICoreAPI api)
        => Instance(api)?.Processes.All ?? System.Array.Empty<ProcessDefinition>();

    public static IReadOnlyCollection<AlmanacEntry> AllEntries(ICoreAPI api)
        => Instance(api)?.Entries.All ?? System.Array.Empty<AlmanacEntry>();

    public static AlmanacEntry? GetEntry(ICoreAPI api, AssetLocation code)
        => Instance(api)?.Entries.Get(code);

    public static IEnumerable<AlmanacEntry> GetVariantsOfGroup(ICoreAPI api, string groupKey)
        => Instance(api)?.Entries.GetVariantsOfGroup(groupKey) ?? System.Array.Empty<AlmanacEntry>();

    public static string GetGroupKey(AssetLocation code) => AlmanacEntry.GetGroupKey(code);

    public static void RecordSight(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Discovery.OnSight(player, code);

    public static void RecordHeld(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Discovery.OnHeld(player, code);

    public static void RecordProcess(IPlayer player, AssetLocation code, string processCode)
        => Instance(player.Entity.Api)?.Discovery.OnProcess(player, code, processCode);

    public static bool HasSeen(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Store.HasSeen(player, code.ToShortString()) == true;

    public static bool HasHeld(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Store.HasHeld(player, code.ToShortString()) == true;

    public static bool HasProcessed(IPlayer player, AssetLocation code, string processCode)
        => Instance(player.Entity.Api)?.Store.HasProcessed(player, code.ToShortString(), processCode) == true;

    public static DiscoveryStage GetStage(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Store.GetStage(player, code.ToShortString()) ?? DiscoveryStage.Unknown;

    public static double? GetSightedAt(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Store.GetSightedAt(player, code.ToShortString());

    public static double? GetHeldAt(IPlayer player, AssetLocation code)
        => Instance(player.Entity.Api)?.Store.GetHeldAt(player, code.ToShortString());

    public static double? GetProcessedAt(IPlayer player, AssetLocation code, string processCode)
        => Instance(player.Entity.Api)?.Store.GetProcessedAt(player, code.ToShortString(), processCode);
}
