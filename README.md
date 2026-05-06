# The Almanac: Codex

The platform mod for [**The Almanac**](https://github.com/Lueken/The-Almanac-VS) — period-faithful vanilla enhancements for [Vintage Story](https://www.vintagestory.at/).

Codex introduces **per-player progressive discovery** for flora, fungi, and recipes. Items don't appear in your Almanac until you've encountered them in the world; their properties stay hidden until you handle them; their applications stay hidden until you actually try a process. A real almanac built up by observation — not a printed catalogue from world-load.

> **Status:** v0.2.0 in development. Discovery API, dialog UI, and the entry-metadata extension are complete. Almanac sub-mods (Forager, Apothecary, Alchemist) register against this platform.

---

## What Codex provides

- **Per-player progressive discovery** with three stages: Sighted → Held → Processed (per process). Server-authoritative, persistent across save/load, scoped per character.
- **Discovery API** that downstream mods register entries + processes against, then call `RecordSight` / `RecordHeld` / `RecordProcess` as players interact.
- **Themed Almanac dialog** (Alt+J) — parchment palette, Georgia serif, wax-seal stage indicators. Index grid sorted by Number / Name / Recency. Detail panel with Latin name, classification, habitat, properties chips, processes, and full description blocks.
- **Cross-mod process registry** — a shared vocabulary all Almanac sub-mods register against (`knap`, `steep`, `ferment`, `dry`, `grind`, etc.).
- **Almanac tab in the vanilla Handbook** — links to the dialog and lists installed sub-mods + their discovery progress.
- **Rich entry metadata** — Latin binomial, classification, habitat, description, plus per-process outcome / flavor / hint text. Each registered AlmanacEntry can carry the lot via init-only properties.
- **Per-discovery timestamps** — every Record* call stamps the in-game day so the dialog can sort by recency and show "first observed" data.

---

## Requirements

- **Vintage Story 1.22.0+**
- **[vsimgui](https://mods.vintagestory.at/vsimgui)** — hard dependency. The Almanac dialog renders through this ImGui binding library. Install via VS's mod manager (Mod Manager → Browse → search `vsimgui` → install) before loading Codex. Codex declares it in `modinfo.json`, so the game refuses to load Codex if vsimgui is missing.

No other mods required. Codex is the *platform* — Almanac sub-mods (Forager, Apothecary, Alchemist) declare Codex as their hard dependency, not the other way around.

---

## In-game

| Action | Effect |
|---|---|
| `Alt+J` | Opens the Almanac dialog. |
| Hover a registered flora/fungus | Codex records a sighting (Sighted stage). |
| Pick one up | Held stage — properties (tags) reveal in the detail panel. |
| Use it in a registered process block | Processed stage — the process card flips from `UNTRIED` to `DONE`. |

Diagnostic chat commands (typed in the chat console):

| Command | Purpose |
|---|---|
| `.codex status` | Print mod status + counts. |
| `.codex list` | List registered entries. |
| `.codex tags` | Print the trait-tag set on the held item or looked-at block. |
| `.codex here` | Print the block at the player's position with full asset code. |

---

## For modders integrating with Codex

```csharp
// In your ModSystem.AssetsFinalize:
foreach (var collectible in api.World.Collectibles)
{
    if (collectible.Code.Domain != "yourmod") continue;
    CodexAPI.RegisterEntry(api, new AlmanacEntry(collectible.Code, "yourmod")
    {
        // All optional — display gracefully degrades if any are absent.
        LatinName        = "Genus species",
        ClassificationKey = "yourmod:codex-class.your-class-slug",
        HabitatKey        = "yourmod:codex-habitat.species-slug",
        DescriptionKey    = "yourmod:codex-description.species-slug",
    });
}

CodexAPI.RegisterProcess(api, new ProcessDefinition(
    code: "yourprocess",
    displayKey: "yourmod:process-yourprocess",
    ownerModId: "yourmod")
{
    OutcomeCode = new AssetLocation("yourmod", "your-product"),
    FlavorKey   = "yourmod:codex-process-flavor.yourprocess",
    HintKey     = "yourmod:codex-process-hint.yourprocess",
});

// At runtime, from your BlockEntity when a recipe completes:
foreach (var ingredient in inputStacks)
{
    CodexAPI.RecordProcess(player, ingredient.Collectible.Code, "yourprocess");
}
```

Sight and pickup are wired automatically — Codex handles them as long as the entry is registered. The `RecordSight` / `RecordHeld` / `RecordProcess` methods route to the server when invoked client-side, so they're safe to call from either side.

---

## Build

```powershell
$env:VINTAGE_STORY = "$env:APPDATA\Vintagestory"
dotnet build
```

Output: `bin/Debug/Mods/AlmanacCodex.dll`. Deploy as a folder mod by copying `modinfo.json`, `AlmanacCodex.dll`, and any `assets/` to `%APPDATA%\VintagestoryData\Mods\almanaccodex\`.

The `.csproj` references VSImGui at compile time via a relative path to `libs/vsimgui/`. If you've cloned this repo via the [vs-workshop](https://github.com/Lueken/VS-Workshop) umbrella, the workshop's `scripts/extract-vsimgui.sh` populates `libs/` from your installed vsimgui mod zip. Otherwise, ensure `VSImGui.dll` and `ImGui.NET.dll` are reachable at the path the .csproj points to.

---

## License

MIT. See [LICENSE](LICENSE).
