# The Almanac: Codex — TODO

Open threads from the Alt+J dialog work session (2026-05-05).

## GUI polish
- [ ] **Brand theming the dialog** — ImGui still looks like a debug UI. Theme via `PushStyleColor` and custom fonts (via `VSImGui.API.FontManager.Load`) to match the schematic / handcrafted Almanac aesthetic. Reference: tfg-companion overlay tooltip styling (paper bg, ink border, IBM Plex Mono, bevelled clip-path corner).
- [ ] **Detail panel polish — pass 1 (structural, this session)** — rebuild around Concept 3 (stage seals): back button row, stage seals top-right (Sighted ✓ / Held ✓ / Processed N/M), specimen icon left, meta block right (placeholders for habitat/latin name), variants row showing known/unknown per direction, process cards with DONE/UNTRIED state. Fields without data show placeholder text or blank. Brand theming applies later.
- [ ] **Detail panel polish — pass 2 (data extension, next session)** — populate the structural placeholders. Requires extending the Codex API:
  - `AlmanacEntry` gains optional fields: `LatinName`, `Classification`, `Habitat`, `Description` (set by Forager at registration)
  - `Process` gains: `OutcomeStack` ("→ dried reishi"), `HintText` ("try a drying rack")
  - `DiscoveryStore` gains timestamps for "First observed" line
  - Forager content pass: populate Latin/habitat/description for the 9 herbs + mushrooms it ships
  - Per-variant discovery state surfaced for the variants row (currently DiscoveryStore is per-code, just need a query)
- [x] ~~**Search box**~~ — done. Top-bar input filters by name + tag.
- [ ] **Sort options** — discovered items alphabetical is fine; consider grouping by track, by chapter, by recency-of-discovery.
- [x] ~~**Category headers in the grid**~~ — done. "Discovered" / "Not yet encountered" headers between sections.

## Known issues
- [x] ~~**Esc doesn't close the Alt+J dialog**~~ — fixed by detecting `ImGuiKey.Escape` directly in `OnDraw` and calling `Close()` + `HandleClosed()` (bypasses the vsimgui Closed event chain that wasn't propagating).
- [x] ~~**Variant block duplicates** (e.g. tree-mounted reishi showing 4 cells, one per direction)~~ — fixed by grouping grid rows by display name. Stage = MAX, tags = UNION, processes = UNION across variants. Counter and detail panel updated.

## Cross-mod investigations
- [ ] **Forager: tag patches inconsistent across variants** — the same reishi block reports different tags on different directional variants (`-north` had `fibrous,medicinal`, `-west` had `fruity,medicinal`). The Forager `tagsByType` JSON patch isn't matching all variants uniformly. Investigate the patch selectors and ensure all `direction` variantgroup values get the same trait tags. Currently masked by Codex's tag-union in the grouped detail panel, but real fix belongs in Forager.

## Verification
- [ ] **Test on a fresh save** — verify sight/pickup/process discovery flows end-to-end with the new GUI in place. Specifically:
  - Hover a flora block → entry appears as Sighted (full color icon, no tags)
  - Pick it up → tags reveal in tooltip + detail panel
  - Use it in a single-process block (e.g. fermentation crock) → process unlocks
  - Persistence across save/reload
  - Multiplayer: independent discovery state per player
- [ ] **Verify silhouette behavior on undiscovered items** — make sure `Stage == Unknown` items show as dark tinted silhouettes with "???" tooltip and "Not yet discovered" detail panel.

## Repo hygiene
- [ ] **Commit + push outstanding work** — codex repo has substantial uncommitted changes from this session (icon overlay, tooltip baking, scissor work, pokédex silhouettes, etc.).
- [ ] **Decide on `libs/vsimgui/` in vs-workshop** — currently uncommitted; either commit the DLLs or gitignore + extract via setup.sh.
