# The Almanac: Codex

The platform mod for [**The Almanac**](https://github.com/Lueken/The-Almanac-VS) — period-faithful vanilla enhancements for [Vintage Story](https://www.vintagestory.at/).

Codex introduces **per-player progressive discovery** for flora, fungi, and recipes. Items don't appear in your Handbook until you've encountered them in the world; their properties stay hidden until you handle them; their applications stay hidden until you actually try a process. A real almanac built up by observation — not a printed catalogue from world-load.

> **Status:** v0.1.0 in development. Foundation API + discovery store + network channel + sight/pickup hooks compile and load. Handbook tab integration is next.

## What Codex provides

- **Discovery API** — downstream mods register entries and processes, then call `RecordSight`/`RecordHeld`/`RecordProcess` as players interact.
- **Three-stage progressive discovery** per player per item: Sighted → Held → Processed (per process).
- **Server-authoritative sync** via vanilla `WatchedAttributes`. Persistent across save/load. Per-character.
- **Process registry** — shared vocabulary across all Almanac sub-mods (`steep`, `ferment`, `knap`, `grind`, `dry`, etc.).
- **Almanac tab in the vanilla Handbook** *(coming in this version)* — entries fill in as players discover them.

## Status of v0.1.0 deliverables

- [x] Discovery API surface (`CodexAPI`)
- [x] `DiscoveryStore` (per-player WatchedAttributes-backed)
- [x] `ProcessRegistry`, `AlmanacEntryRegistry`
- [x] Network channel `almanaccodex.discovery` with three packet types
- [x] Sight detector (client crosshair → server) with per-position dedupe
- [x] Pickup hook (server-side `SlotModified` listener)
- [ ] Handbook tab integration (investigation phase + implementation)
- [ ] Tests with a sample dependent mod

## For modders integrating with Codex

```csharp
// In your ModSystem.AssetsFinalize:
foreach (var collectible in api.World.Collectibles)
{
    if (collectible.Code.Domain != "yourmod") continue;
    CodexAPI.RegisterEntry(api, new AlmanacEntry(collectible.Code, "yourmod"));
}

CodexAPI.RegisterProcess(api, new ProcessDefinition(
    code: "yourprocess",
    displayKey: "yourmod:process-yourprocess",
    ownerModId: "yourmod"));

// At runtime, from your BlockEntity when a recipe completes:
foreach (var ingredient in inputStacks)
{
    CodexAPI.RecordProcess(player, ingredient.Collectible.Code, "yourprocess");
}
```

Sight and pickup are wired automatically — Codex handles them as long as the entry is registered.

## Requirements

- Vintage Story 1.22.0+
- No required mods. Codex ships standalone. Almanac sub-mods (Forager, Apothecary, Alchemist) declare it as a hard dependency from v0.2.0 onward.

## Build

```powershell
$env:VINTAGE_STORY = "$env:APPDATA\Vintagestory"
dotnet build
```

Output: `bin/Debug/Mods/AlmanacCodex.dll`. Deploy as a folder mod by copying `modinfo.json`, `AlmanacCodex.dll`, and any `assets/` to `%APPDATA%\VintagestoryData\Mods\almanaccodex\`.

## License

- **Code:** MIT
- **Assets:** CC-BY-NC-SA 4.0
