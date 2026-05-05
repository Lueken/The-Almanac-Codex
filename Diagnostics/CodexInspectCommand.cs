using System.Linq;
using System.Text;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AlmanacCodex.Diagnostics;

public static class CodexInspectCommand
{
    public static void Register(ICoreClientAPI capi, AlmanacCodexModSystem mod)
    {
        capi.ChatCommands.Create("codex")
            .WithDescription("The Almanac: Codex diagnostics")
            .BeginSubCommand("status")
                .WithDescription("Print registered entry/process counts and your discovery totals")
                .HandleWith(_ => Status(capi, mod))
            .EndSubCommand()
            .BeginSubCommand("here")
                .WithDescription("Print discovery stage for the looked-at block or held item")
                .HandleWith(_ => Here(capi, mod))
            .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("List your discovered entries with stage")
                .HandleWith(_ => ListDiscovered(capi, mod))
            .EndSubCommand();
    }

    private static TextCommandResult Status(ICoreClientAPI capi, AlmanacCodexModSystem mod)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Codex registry:");
        sb.AppendLine($"  entries: {mod.Entries.Count}");
        sb.AppendLine($"  processes: {mod.Processes.All.Count}");
        var procs = string.Join(", ", mod.Processes.All.Select(p => p.Code));
        sb.AppendLine($"  process codes: [{procs}]");

        var player = capi.World.Player;
        int seen = 0, held = 0, processed = 0;
        foreach (var entry in mod.Entries.All)
        {
            var stage = mod.Store.GetStage(player, entry.Code.ToShortString());
            if (stage >= DiscoveryStage.Sighted) seen++;
            if (stage >= DiscoveryStage.Held) held++;
            if (stage >= DiscoveryStage.Processed) processed++;
        }
        sb.AppendLine($"Your discoveries:");
        sb.AppendLine($"  sighted: {seen}");
        sb.AppendLine($"  held: {held}");
        sb.AppendLine($"  processed: {processed}");
        return TextCommandResult.Success(sb.ToString());
    }

    private static TextCommandResult Here(ICoreClientAPI capi, AlmanacCodexModSystem mod)
    {
        AssetLocation? code = capi.World.Player.CurrentBlockSelection?.Position is { } pos
            ? capi.World.BlockAccessor.GetBlock(pos)?.Code
            : null;
        if (code == null || code.Path == "air")
        {
            code = capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code;
        }
        if (code == null) return TextCommandResult.Success("Nothing to inspect.");

        var registered = mod.Entries.IsRegistered(code);
        var stage = mod.Store.GetStage(capi.World.Player, code.ToShortString());
        var processes = mod.Store.ProcessesUnlocked(capi.World.Player, code.ToShortString()).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"Code: {code}");
        sb.AppendLine($"  registered: {registered}");
        sb.AppendLine($"  stage: {stage}");
        sb.AppendLine($"  processes unlocked: {(processes.Length == 0 ? "(none)" : string.Join(", ", processes))}");
        return TextCommandResult.Success(sb.ToString());
    }

    private static TextCommandResult ListDiscovered(ICoreClientAPI capi, AlmanacCodexModSystem mod)
    {
        var player = capi.World.Player;
        var sb = new StringBuilder();
        int n = 0;
        foreach (var entry in mod.Entries.All)
        {
            var stage = mod.Store.GetStage(player, entry.Code.ToShortString());
            if (stage == DiscoveryStage.Unknown) continue;
            sb.AppendLine($"  [{stage}] {entry.Code}");
            n++;
            if (n >= 50) { sb.AppendLine($"  ... ({mod.Entries.Count - n} more)"); break; }
        }
        if (n == 0) return TextCommandResult.Success("No discoveries yet.");
        return TextCommandResult.Success($"Discovered {n} entries:\n{sb}");
    }
}
