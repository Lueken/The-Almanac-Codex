using AlmanacCodex.Networking;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AlmanacCodex.Discovery;

public class DiscoveryService
{
    private readonly ICoreAPI api;
    private readonly DiscoveryStore store;
    private readonly AlmanacEntryRegistry entries;
    private readonly ProcessRegistry processes;

    public DiscoveryService(ICoreAPI api, DiscoveryStore store, AlmanacEntryRegistry entries, ProcessRegistry processes)
    {
        this.api = api;
        this.store = store;
        this.entries = entries;
        this.processes = processes;
    }

    public void OnSight(IPlayer player, AssetLocation code)
    {
        if (api.Side == EnumAppSide.Client)
        {
            ((ICoreClientAPI)api).Network.GetChannel(NetworkChannels.Discovery)
                .SendPacket(new SightPacket { Code = code.ToShortString() });
            return;
        }
        ProcessSightServer(player, code.ToShortString());
    }

    public void OnHeld(IPlayer player, AssetLocation code)
    {
        if (api.Side == EnumAppSide.Client)
        {
            ((ICoreClientAPI)api).Network.GetChannel(NetworkChannels.Discovery)
                .SendPacket(new HeldPacket { Code = code.ToShortString() });
            return;
        }
        ProcessHeldServer(player, code.ToShortString());
    }

    public void OnProcess(IPlayer player, AssetLocation code, string processCode)
    {
        if (api.Side == EnumAppSide.Client)
        {
            ((ICoreClientAPI)api).Network.GetChannel(NetworkChannels.Discovery)
                .SendPacket(new ProcessPacket { Code = code.ToShortString(), ProcessCode = processCode });
            return;
        }
        ProcessProcessServer(player, code.ToShortString(), processCode);
    }

    public void ProcessSightServer(IPlayer player, string code)
    {
        if (!entries.IsRegistered(new AssetLocation(code))) return;
        if (store.RecordSight(player, code))
        {
            CodexLogger.Debug(api, "discovery",
                $"sight '{code}' player='{player.PlayerName}'");
        }
    }

    public void ProcessHeldServer(IPlayer player, string code)
    {
        if (!entries.IsRegistered(new AssetLocation(code))) return;
        if (store.RecordHeld(player, code))
        {
            CodexLogger.Info(api, "discovery",
                $"HELD '{code}' player='{player.PlayerName}'");
        }
    }

    public void ProcessProcessServer(IPlayer player, string code, string processCode)
    {
        if (!entries.IsRegistered(new AssetLocation(code)))
        {
            CodexLogger.Debug(api, "discovery",
                $"process '{processCode}' on unregistered code '{code}' (skipped)");
            return;
        }
        if (!processes.IsRegistered(processCode))
        {
            CodexLogger.Warn(api, "discovery",
                $"process '{processCode}' is not registered; record dropped (code='{code}', player='{player.PlayerName}')");
            return;
        }
        if (store.RecordProcess(player, code, processCode))
        {
            CodexLogger.Info(api, "discovery",
                $"PROCESS '{processCode}' on '{code}' player='{player.PlayerName}'");
        }
    }
}
