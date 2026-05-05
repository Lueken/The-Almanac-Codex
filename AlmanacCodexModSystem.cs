using AlmanacCodex.Diagnostics;
using AlmanacCodex.Discovery;
using AlmanacCodex.Gui;
using AlmanacCodex.Handbook;
using AlmanacCodex.Networking;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

[assembly: ModInfo("The Almanac: Codex", "almanaccodex",
    Authors = new string[] { "Lueken Good Design" },
    Description = "Per-player progressive discovery system for The Almanac line.",
    Version = "0.1.0")]

namespace AlmanacCodex;

public class AlmanacCodexModSystem : ModSystem
{
    public ProcessRegistry Processes { get; private set; } = null!;
    public AlmanacEntryRegistry Entries { get; private set; } = null!;
    public DiscoveryStore Store { get; private set; } = null!;
    public DiscoveryService Discovery { get; private set; } = null!;

    private SightDetector? sightDetector;
    private PickupHook? pickupHook;
    private HandbookIntegration? handbookIntegration;
    private AlmanacDialog? almanacDialog;

    public override void Start(ICoreAPI api)
    {
        CodexLogger.Info(api, "mod-system", $"loading The Almanac: Codex v0.1.0 (side={api.Side})");

        Processes = new ProcessRegistry(api);
        Entries = new AlmanacEntryRegistry(api);
        Store = new DiscoveryStore(api);
        Discovery = new DiscoveryService(api, Store, Entries, Processes);

        api.Network.RegisterChannel(NetworkChannels.Discovery)
            .RegisterMessageType<SightPacket>()
            .RegisterMessageType<HeldPacket>()
            .RegisterMessageType<ProcessPacket>();

        CodexLogger.Info(api, "mod-system",
            $"network channel '{NetworkChannels.Discovery}' registered with 3 packet types");
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        var channel = sapi.Network.GetChannel(NetworkChannels.Discovery);
        channel.SetMessageHandler<SightPacket>((player, packet) =>
            Discovery.ProcessSightServer(player, packet.Code));
        channel.SetMessageHandler<HeldPacket>((player, packet) =>
            Discovery.ProcessHeldServer(player, packet.Code));
        channel.SetMessageHandler<ProcessPacket>((player, packet) =>
            Discovery.ProcessProcessServer(player, packet.Code, packet.ProcessCode));

        pickupHook = new PickupHook(sapi, Discovery, Entries);

        CodexLogger.Info(sapi, "mod-system", "server handlers + pickup hook ready");
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        sightDetector = new SightDetector(capi, Discovery, Entries);
        CodexInspectCommand.Register(capi, this);
        handbookIntegration = new HandbookIntegration(capi, Entries, Store);

        almanacDialog = new AlmanacDialog(capi, Entries, Store);
        capi.Input.RegisterHotKey(
            "almanaccodex-open",
            Lang.Get("almanaccodex:hotkey-open"),
            GlKeys.J,
            HotkeyType.GUIOrOtherControls,
            altPressed: true);
        capi.Input.SetHotKeyHandler("almanaccodex-open", _ => ToggleDialog());

        CodexLogger.Info(capi, "mod-system",
            "client sight detector + .codex chat command + Handbook integration + Alt+J dialog ready");
    }

    private bool ToggleDialog()
    {
        if (almanacDialog == null) return false;
        almanacDialog.Toggle();
        return true;
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        CodexLogger.Info(api, "mod-system",
            $"assets finalized: {Entries.Count} entries registered, {Processes.All.Count} processes registered");
    }
}
