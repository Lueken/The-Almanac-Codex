using AlmanacCodex.Registry;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AlmanacCodex.Discovery;

/// <summary>
/// Server-side. Hooks SlotModified on every player and emits a held discovery
/// when a registered entry's itemstack appears in inventory for the first time.
/// </summary>
public class PickupHook
{
    private readonly ICoreServerAPI sapi;
    private readonly DiscoveryService discovery;
    private readonly AlmanacEntryRegistry entries;

    public PickupHook(ICoreServerAPI sapi, DiscoveryService discovery, AlmanacEntryRegistry entries)
    {
        this.sapi = sapi;
        this.discovery = discovery;
        this.entries = entries;

        sapi.Event.PlayerJoin += OnPlayerJoin;
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        foreach (var inv in player.InventoryManager.Inventories.Values)
        {
            Hook(player, inv);
        }
    }

    private void Hook(IServerPlayer player, IInventory inv)
    {
        inv.SlotModified += slotId =>
        {
            var stack = inv[slotId]?.Itemstack;
            var code = stack?.Collectible?.Code;
            if (code == null) return;
            if (!entries.IsRegistered(code)) return;
            discovery.ProcessHeldServer(player, code.ToShortString());
        };
    }
}
