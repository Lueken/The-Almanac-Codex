using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AlmanacCodex.State;

public class DiscoveryStore
{
    private const string RootKey = "almanac-codex";
    private const string SeenKey = "seen";
    private const string HeldKey = "held";
    private const string ProcessedKey = "processed";
    private const string SeenAtKey = "seenAt";
    private const string HeldAtKey = "heldAt";
    private const string ProcessedAtKey = "processedAt";

    private readonly ICoreAPI api;

    public DiscoveryStore(ICoreAPI api)
    {
        this.api = api;
    }

    private static ITreeAttribute Root(IPlayer player)
    {
        var root = player.Entity.WatchedAttributes.GetTreeAttribute(RootKey);
        if (root != null) return root;
        var fresh = new TreeAttribute();
        player.Entity.WatchedAttributes[RootKey] = fresh;
        return fresh;
    }

    private static ITreeAttribute SubTree(ITreeAttribute root, string key)
    {
        var sub = root.GetTreeAttribute(key);
        if (sub != null) return sub;
        var fresh = new TreeAttribute();
        root[key] = fresh;
        return fresh;
    }

    public bool RecordSight(IPlayer player, string code, double? totalDays = null)
    {
        var root = Root(player);
        var seen = SubTree(root, SeenKey);
        if (seen.HasAttribute(code)) return false;
        seen.SetBool(code, true);
        if (totalDays.HasValue)
        {
            SubTree(root, SeenAtKey).SetDouble(code, totalDays.Value);
        }
        MarkDirty(player);
        return true;
    }

    public bool RecordHeld(IPlayer player, string code, double? totalDays = null)
    {
        var root = Root(player);
        var held = SubTree(root, HeldKey);
        var seen = SubTree(root, SeenKey);

        bool changed = false;
        if (!seen.HasAttribute(code))
        {
            seen.SetBool(code, true);
            if (totalDays.HasValue) SubTree(root, SeenAtKey).SetDouble(code, totalDays.Value);
            changed = true;
        }
        if (!held.HasAttribute(code))
        {
            held.SetBool(code, true);
            if (totalDays.HasValue) SubTree(root, HeldAtKey).SetDouble(code, totalDays.Value);
            changed = true;
        }
        if (changed) MarkDirty(player);
        return changed;
    }

    public bool RecordProcess(IPlayer player, string code, string processCode, double? totalDays = null)
    {
        var root = Root(player);
        var processed = SubTree(root, ProcessedKey);
        var perItem = processed.GetTreeAttribute(code);
        if (perItem == null)
        {
            perItem = new TreeAttribute();
            processed[code] = perItem;
        }
        if (perItem.HasAttribute(processCode)) return false;
        perItem.SetBool(processCode, true);
        if (totalDays.HasValue)
        {
            var processedAt = SubTree(root, ProcessedAtKey);
            var perItemAt = processedAt.GetTreeAttribute(code);
            if (perItemAt == null)
            {
                perItemAt = new TreeAttribute();
                processedAt[code] = perItemAt;
            }
            perItemAt.SetDouble(processCode, totalDays.Value);
        }
        MarkDirty(player);
        return true;
    }

    public bool HasSeen(IPlayer player, string code)
        => Root(player).GetTreeAttribute(SeenKey)?.HasAttribute(code) == true;

    public bool HasHeld(IPlayer player, string code)
        => Root(player).GetTreeAttribute(HeldKey)?.HasAttribute(code) == true;

    public bool HasProcessed(IPlayer player, string code, string processCode)
        => Root(player).GetTreeAttribute(ProcessedKey)?
            .GetTreeAttribute(code)?
            .HasAttribute(processCode) == true;

    public DiscoveryStage GetStage(IPlayer player, string code)
    {
        var root = Root(player);
        if (root.GetTreeAttribute(ProcessedKey)?.GetTreeAttribute(code)?.Count > 0) return DiscoveryStage.Processed;
        if (root.GetTreeAttribute(HeldKey)?.HasAttribute(code) == true) return DiscoveryStage.Held;
        if (root.GetTreeAttribute(SeenKey)?.HasAttribute(code) == true) return DiscoveryStage.Sighted;
        return DiscoveryStage.Unknown;
    }

    public IEnumerable<string> ProcessesUnlocked(IPlayer player, string code)
    {
        var perItem = Root(player).GetTreeAttribute(ProcessedKey)?.GetTreeAttribute(code);
        if (perItem == null) yield break;
        foreach (var key in perItem)
        {
            yield return key.Key;
        }
    }

    public double? GetSightedAt(IPlayer player, string code)
    {
        var sub = Root(player).GetTreeAttribute(SeenAtKey);
        return sub != null && sub.HasAttribute(code) ? sub.GetDouble(code) : null;
    }

    public double? GetHeldAt(IPlayer player, string code)
    {
        var sub = Root(player).GetTreeAttribute(HeldAtKey);
        return sub != null && sub.HasAttribute(code) ? sub.GetDouble(code) : null;
    }

    public double? GetProcessedAt(IPlayer player, string code, string processCode)
    {
        var perItem = Root(player).GetTreeAttribute(ProcessedAtKey)?.GetTreeAttribute(code);
        return perItem != null && perItem.HasAttribute(processCode) ? perItem.GetDouble(processCode) : null;
    }

    private static void MarkDirty(IPlayer player)
    {
        player.Entity.WatchedAttributes.MarkPathDirty(RootKey);
    }
}
