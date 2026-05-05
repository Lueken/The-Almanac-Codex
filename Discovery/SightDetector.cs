using AlmanacCodex.Registry;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AlmanacCodex.Discovery;

/// <summary>
/// Client-side. Watches the player's CurrentBlockSelection. When the crosshair
/// lands on a block whose code is a registered Almanac entry, fires a sight
/// event at the server. Per-frame call but rate-limited to position transitions.
/// </summary>
public class SightDetector
{
    private readonly ICoreClientAPI capi;
    private readonly DiscoveryService discovery;
    private readonly AlmanacEntryRegistry entries;

    private BlockPos? lastPos;

    public SightDetector(ICoreClientAPI capi, DiscoveryService discovery, AlmanacEntryRegistry entries)
    {
        this.capi = capi;
        this.discovery = discovery;
        this.entries = entries;

        capi.Event.RegisterGameTickListener(OnTick, 100);
    }

    private void OnTick(float dt)
    {
        var sel = capi.World.Player?.CurrentBlockSelection;
        var pos = sel?.Position;
        if (pos == null) { lastPos = null; return; }

        if (lastPos != null && lastPos.Equals(pos)) return;
        lastPos = pos.Copy();

        var block = capi.World.BlockAccessor.GetBlock(pos);
        if (block?.Code == null) return;

        if (!entries.IsRegistered(block.Code)) return;
        discovery.OnSight(capi.World.Player, block.Code);
    }
}
