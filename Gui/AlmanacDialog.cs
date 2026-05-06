using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using ImGuiNET;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using VSImGui;
using VSImGui.API;

namespace AlmanacCodex.Gui;

public class AlmanacDialog : ImGuiDialogWindow
{
    private const string WindowIdValue = "almanaccodex.main";

    private readonly ICoreClientAPI capi;
    private readonly AlmanacEntryRegistry entries;
    private readonly DiscoveryStore store;
    private readonly ProcessRegistry processes;
    private readonly IconOverlayDialog iconOverlay;

    // Shared with vanilla handbook: setting "noHandbookPause" — false (default) = pause game, true = don't pause.
    private const string PauseSettingKey = "noHandbookPause";

    private string activeTagFilter = "";
    private string? selectedEntryCode;
    private bool isOpen;
    private float outerDialogTop;
    private float outerDialogBottom;
    private string searchQuery = "";

    public void Toggle()
    {
        if (isOpen)
        {
            Close();
            HandleClosed();
        }
        else
        {
            Open();
            isOpen = true;
            // Force VSImGui's host VS dialog to open (grabs mouse + accepts inputs).
            capi.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>()?.Show();
            ApplyPauseFromSetting();
            if (!iconOverlay.IsOpened()) iconOverlay.TryOpen();
        }
    }

    protected override bool OnClose()
    {
        // Fires when VSImGui closes us externally — typically Esc key.
        HandleClosed();
        return base.OnClose();
    }

    private void HandleClosed()
    {
        if (!isOpen) return;
        isOpen = false;
        UnpauseIfWasPaused();
        if (iconOverlay.IsOpened()) iconOverlay.TryClose();
    }

    public AlmanacDialog(ICoreClientAPI capi, AlmanacEntryRegistry entries, DiscoveryStore store, ProcessRegistry processes)
        : base(capi, Lang.Get("almanaccodex:dialog-title"), WindowIdValue, includeTitleIntoId: false,
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;
        this.processes = processes;
        this.iconOverlay = new IconOverlayDialog(capi);

        // VSImGui exposes GrabMouse as get-only publicly, but the underlying field controls
        // whether the host VS dialog releases the cursor. Without this the cursor stays locked
        // even with the dialog open. Set it via reflection (private setter or backing field).
        SetGrabMouseTrue();
    }

    private void SetGrabMouseTrue()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var prop = typeof(ImGuiDialogBase).GetProperty("GrabMouse", flags);
        if (prop != null && prop.GetSetMethod(nonPublic: true) != null)
        {
            prop.SetValue(this, true);
            return;
        }
        var field = typeof(ImGuiDialogBase).GetField("_grabMouse", flags);
        field?.SetValue(this, true);
    }


    private bool ShouldPauseSetting
    {
        get => !capi.Settings.Bool[PauseSettingKey];
        set => capi.Settings.Bool[PauseSettingKey] = !value;
    }

    private void ApplyPauseFromSetting()
    {
        if (ShouldPauseSetting) capi.PauseGame(true);
    }

    private void UnpauseIfWasPaused()
    {
        if (ShouldPauseSetting) capi.PauseGame(false);
    }

    protected override bool OnDraw()
    {
        ImGui.SetWindowSize(new System.Numerics.Vector2(880, 560), ImGuiCond.FirstUseEver);

        // Esc to close. ImGuiDialogBase claims to handle ImGuiModSystem.Closed automatically,
        // but in practice the event isn't reaching us — likely because vsimgui's host VS dialog
        // consumes Esc before propagating. Detect the key in our own draw pass and shut down.
        if (ImGui.IsKeyPressed(ImGuiKey.Escape, repeat: false))
        {
            Close();
            HandleClosed();
            return false;
        }

        // Reset per-frame icon render queue. The IconOverlayDialog drains this list during
        // its OnRenderGUI on the same frame.
        iconOverlay.Requests.Clear();

        // Capture the outer dialog rect now (we're inside the main ImGui window, before any
        // BeginChild). DrawGrid uses these bounds as a manual visibility gate (the engine's
        // PushScissor doesn't fully clip the itemstack render path).
        var outerPos = ImGui.GetWindowPos();
        var outerSize = ImGui.GetWindowSize();
        outerDialogTop = outerPos.Y;
        outerDialogBottom = outerPos.Y + outerSize.Y;
        iconOverlay.ClipBounds = new ClipRect(outerPos.X, outerPos.Y, outerSize.X, outerSize.Y);

        DrawTopBar();
        ImGui.Separator();

        if (selectedEntryCode != null)
        {
            DrawDetailPanel(selectedEntryCode);
            return Opened;
        }

        var avail = ImGui.GetContentRegionAvail();
        if (ImGui.BeginChild("almanac.sidebar", new System.Numerics.Vector2(180, avail.Y), border: true))
        {
            DrawSidebar();
        }
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("almanac.grid", new System.Numerics.Vector2(0, avail.Y)))
        {
            DrawGrid();
        }
        ImGui.EndChild();

        return Opened;
    }

    private void DrawTopBar()
    {
        var player = capi.World.Player;
        // Count by orientation-stripped group key so the counter matches what the player
        // sees in the grid (variant blocks are folded into one entry).
        var groupStages = new Dictionary<string, DiscoveryStage>();
        foreach (var e in entries.All)
        {
            var groupKey = GroupKey(e.Code);
            var stage = store.GetStage(player, e.Code.ToShortString());
            if (!groupStages.TryGetValue(groupKey, out var prev) || stage > prev) groupStages[groupKey] = stage;
        }
        int total = groupStages.Count;
        int discovered = 0;
        foreach (var s in groupStages.Values) if (s != DiscoveryStage.Unknown) discovered++;

        // When a detail panel is open, the top bar leads with a Back button instead of the
        // counter so the user always has an exit at the same screen position.
        if (selectedEntryCode != null)
        {
            if (ImGui.Button(Lang.Get("almanaccodex:detail-back"))) selectedEntryCode = null;
            ImGui.SameLine();
        }
        ImGui.TextUnformatted(Lang.Get("almanaccodex:dialog-counter", discovered, total));

        // Search input — middle of top bar.
        ImGui.SameLine();
        ImGui.SetCursorPosX(280);
        ImGui.SetNextItemWidth(280);
        ImGui.InputTextWithHint("##almanac.search", Lang.Get("almanaccodex:search-hint"), ref searchQuery, 64);
        if (searchQuery.Length > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("X##almanac.search.clear")) searchQuery = "";
        }

        // Pause checkbox — right side.
        ImGui.SameLine();
        var pauseFlag = ShouldPauseSetting;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 160);
        if (ImGui.Checkbox(Lang.Get("almanaccodex:dialog-pause"), ref pauseFlag))
        {
            ShouldPauseSetting = pauseFlag;
            capi.PauseGame(pauseFlag);
        }
    }

    private void DrawSidebar()
    {
        if (ImGui.Selectable(Lang.Get("almanaccodex:filter-all"), activeTagFilter == ""))
        {
            activeTagFilter = "";
        }
        ImGui.Separator();
        foreach (var tag in TagFilters.All)
        {
            var label = Lang.Get("almanaccodex:filter-" + tag.Slug);
            if (ImGui.Selectable(label, activeTagFilter == tag.Code))
            {
                activeTagFilter = tag.Code;
            }
        }
    }

    private const float CellSize = 64f;
    private const float CellPad = 6f;
    // RenderItemstackToGui draws icons larger than the size param suggests in ImGui's coord
    // space. Use ~50% of the cell for the icon "size" and center it within the cell box.
    private const float IconSize = 32f;
    private const uint TintNormal = 0xFFFFFFFF;        // ABGR — opaque white
    private const uint BorderHover = 0x80FFFFFFu;      // 50% white
    private const uint BgCellNormal = 0x40202020u;     // ~25% dark
    private const uint BgCellHover = 0x80303030u;

    private void DrawGrid()
    {
        var player = capi.World.Player;
        var rows = BuildGridRows(player);

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(Lang.Get("almanaccodex:grid-empty"));
            return;
        }

        // Capture the grid child's screen rect so cells that scroll off top/bottom of the
        // child don't have their icons render onto the top bar / outside the dialog.
        var gridWinPos = ImGui.GetWindowPos();
        var gridWinSize = ImGui.GetWindowSize();
        var gridTop = gridWinPos.Y;
        var gridBottom = gridWinPos.Y + gridWinSize.Y;

        var avail = ImGui.GetContentRegionAvail();
        int cols = System.Math.Max(1, (int)((avail.X + CellPad) / (CellSize + CellPad)));
        var drawList = ImGui.GetWindowDrawList();

        // Render a "Discovered" header above the first row only if there's at least one
        // discovered entry; same for "Undiscovered" before the first silhouette.
        bool hasDiscovered = rows.Any(r => r.Stage != DiscoveryStage.Unknown);
        bool renderedDiscoveredHeader = !hasDiscovered;
        bool renderedUndiscoveredHeader = false;

        int i = 0;
        foreach (var row in rows)
        {
            // Section headers between discovered and undiscovered groups.
            if (!renderedDiscoveredHeader && row.Stage != DiscoveryStage.Unknown)
            {
                DrawSectionHeader(Lang.Get("almanaccodex:section-discovered"));
                renderedDiscoveredHeader = true;
                i = 0;
            }
            if (!renderedUndiscoveredHeader && row.Stage == DiscoveryStage.Unknown)
            {
                if (i > 0) ImGui.NewLine();
                DrawSectionHeader(Lang.Get("almanaccodex:section-undiscovered"));
                renderedUndiscoveredHeader = true;
                i = 0;
            }

            if (i % cols != 0) ImGui.SameLine(0, CellPad);

            // Reserve cell space + capture screen rect, use InvisibleButton for hit testing.
            var topLeft = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"cell##{row.CodeKey}", new System.Numerics.Vector2(CellSize, CellSize));
            bool hovered = ImGui.IsItemHovered();
            bool clicked = ImGui.IsItemClicked();
            var bottomRight = new System.Numerics.Vector2(topLeft.X + CellSize, topLeft.Y + CellSize);

            // Manual visibility gate: only render icon if the entire cell sits within the
            // grid child window's screen rect. Catches both top-edge bleed (cell scrolled
            // up into the top bar) and bottom-edge bleed (cell scrolled below dialog).
            bool visible = ImGui.IsItemVisible()
                && bottomRight.Y <= gridBottom
                && topLeft.Y >= gridTop;

            if (visible)
            {
                drawList.AddRectFilled(topLeft, bottomRight, hovered ? BgCellHover : BgCellNormal, 3f);

                var stack = ResolveStack(new AssetLocation(row.CodeKey));
                if (stack != null)
                {
                    var iconLeft = topLeft.X + (CellSize - IconSize) / 2f;
                    var iconTop = topLeft.Y + (CellSize - IconSize) / 2f;
                    int color = row.Stage == DiscoveryStage.Unknown ? IconArgbSilhouette : IconArgbNormal;
                    iconOverlay.Requests.Add(new IconRenderRequest(stack, iconLeft, iconTop, IconSize, color));
                }

                if (hovered) drawList.AddRect(topLeft, bottomRight, BorderHover, 3f, ImDrawFlags.None, 1.5f);
            }

            if (clicked) selectedEntryCode = row.CodeKey;

            if (hovered)
            {
                // Build tooltip lines and hand off to IconOverlayDialog, which renders them
                // in its post-icon pass so they sit on top of icons rather than under them.
                var lines = new List<TooltipLine>();
                if (row.Stage == DiscoveryStage.Unknown)
                {
                    lines.Add(new TooltipLine("???", false));
                    lines.Add(new TooltipLine(Lang.Get("almanaccodex:tooltip-undiscovered"), true));
                }
                else
                {
                    lines.Add(new TooltipLine(row.Name, false));
                    lines.Add(new TooltipLine(row.CodeKey, true));
                    lines.Add(new TooltipLine(Lang.Get("almanaccodex:tooltip-stage-" + row.Stage.ToString().ToLowerInvariant()), true));
                    if (row.Tags.Length > 0) lines.Add(new TooltipLine(string.Join(", ", row.Tags), true));
                }
                var mouse = ImGui.GetMousePos();
                iconOverlay.Tooltip = new TooltipState(lines.ToArray(), mouse.X, mouse.Y);
            }

            i++;
        }
    }

    private const int IconArgbNormal = unchecked((int)0xFFFFFFFFu);
    private const int IconArgbSilhouette = unchecked((int)0xFF202020u);

    private static void DrawSectionHeader(string text)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawDetailPanel(string canonicalCodeKey)
    {
        // Re-derive the group via the orientation-stripped key so stage/tags/processes
        // aggregate over every variant the player has interacted with.
        var canonicalEntry = entries.All.FirstOrDefault(e => e.Code.ToShortString() == canonicalCodeKey);
        if (canonicalEntry == null)
        {
            selectedEntryCode = null;
            return;
        }
        var canonicalGroupKey = GroupKey(canonicalEntry.Code);

        var player = capi.World.Player;
        var groupCodes = new List<string>();
        var groupStage = DiscoveryStage.Unknown;
        var aggregatedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aggregatedProcesses = new HashSet<string>(StringComparer.Ordinal);
        string displayName = canonicalCodeKey;
        bool foundCleanName = false;

        foreach (var e in entries.All)
        {
            if (GroupKey(e.Code) != canonicalGroupKey) continue;

            var stack = ResolveStack(e.Code);
            var thisName = stack?.GetName() ?? e.Code.ToShortString();
            // Prefer a clean display name over the code fallback.
            bool looksLikeCode = thisName.Contains(':') || thisName.StartsWith("block-") || thisName.StartsWith("item-");
            if (!foundCleanName && !looksLikeCode) { displayName = thisName; foundCleanName = true; }
            else if (!foundCleanName) displayName = thisName;

            var thisKey = e.Code.ToShortString();
            groupCodes.Add(thisKey);
            var thisStage = store.GetStage(player, thisKey);
            if (thisStage > groupStage) groupStage = thisStage;

            if (thisStage >= DiscoveryStage.Held && stack != null)
            {
                foreach (var t in capi.CollectibleTagRegistry.SlowEnumerateTagNames(stack.Collectible.Tags))
                    aggregatedTags.Add(t);
            }
            foreach (var p in store.ProcessesUnlocked(player, thisKey)) aggregatedProcesses.Add(p);
        }

        // Card header: title (left, bold) + stage seals (right). Back lives in the top bar.
        ImGui.TextUnformatted(groupStage == DiscoveryStage.Unknown ? "???" : displayName);
        DrawStageSeals(groupStage, aggregatedProcesses.Count, processes.All.Count);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (groupStage == DiscoveryStage.Unknown)
        {
            DrawDetailIcon(canonicalEntry.Code, IconArgbSilhouette);
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextUnformatted("???");
            ImGui.TextDisabled(Lang.Get("almanaccodex:tooltip-undiscovered"));
            ImGui.EndGroup();
            ImGui.Separator();
            ImGui.TextWrapped(Lang.Get("almanaccodex:detail-undiscovered-body"));
            return;
        }

        // Body row: icon (left) + meta block (right). Title is already drawn in the header
        // above; this block holds habitat / properties / variants.
        DrawDetailIcon(canonicalEntry.Code, IconArgbNormal);
        ImGui.SameLine();
        ImGui.BeginGroup();

        // Description: vanilla pattern is `block-{path}-desc` or `item-{path}-desc`.
        string? desc = null;
        foreach (var code in groupCodes)
        {
            var colonIdx = code.IndexOf(':');
            var path = colonIdx >= 0 ? code.Substring(colonIdx + 1) : code;
            if (Lang.HasTranslation("block-" + path + "-desc")) { desc = Lang.Get("block-" + path + "-desc"); break; }
            if (Lang.HasTranslation("item-" + path + "-desc")) { desc = Lang.Get("item-" + path + "-desc"); break; }
        }
        if (!string.IsNullOrEmpty(desc)) ImGui.TextWrapped(desc);
        else ImGui.TextDisabled(Lang.Get("almanaccodex:detail-habitat-placeholder"));

        ImGui.Spacing();
        ImGui.TextDisabled(Lang.Get("almanaccodex:detail-properties-heading"));
        if (aggregatedTags.Count == 0) ImGui.TextDisabled("(none recorded)");
        else DrawTagChips(aggregatedTags);

        ImGui.EndGroup();

        // Process cards section.
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(Lang.Get("almanaccodex:detail-processes-heading-progress",
            aggregatedProcesses.Count, processes.All.Count));
        DrawProcessCards(aggregatedProcesses);
    }

    private void DrawStageSeals(DiscoveryStage stage, int processesDone, int processesTotal)
    {
        ImGui.SameLine();
        // Right-align the seals within the dialog.
        var sealWidth = 78f;
        var totalWidth = sealWidth * 3 + 16f;
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - totalWidth - 12);
        DrawSeal("Sighted", stage >= DiscoveryStage.Sighted, null);
        ImGui.SameLine();
        DrawSeal("Held", stage >= DiscoveryStage.Held, null);
        ImGui.SameLine();
        DrawSeal("Processed", processesDone > 0, $"{processesDone}/{processesTotal}");
    }

    private void DrawSeal(string label, bool unlocked, string? subLabel)
    {
        // Render as a flat colored "chip" for v0.1 (theming pass replaces with custom textures).
        var pos = ImGui.GetCursorScreenPos();
        const float w = 78f, h = 44f;
        ImGui.Dummy(new System.Numerics.Vector2(w, h));
        var dl = ImGui.GetWindowDrawList();
        uint border = unlocked ? 0xFF8FB9A8u : 0xFF555555u;
        uint fill = unlocked ? 0x4040A085u : 0x40303030u;
        uint text = unlocked ? 0xFFE7DBC0u : 0xFF888888u;
        dl.AddRectFilled(pos, new System.Numerics.Vector2(pos.X + w, pos.Y + h), fill, 4f);
        dl.AddRect(pos, new System.Numerics.Vector2(pos.X + w, pos.Y + h), border, 4f, ImDrawFlags.None, 1.5f);
        var textSize = ImGui.CalcTextSize(label);
        dl.AddText(new System.Numerics.Vector2(pos.X + (w - textSize.X) / 2, pos.Y + 6), text, label);
        if (subLabel != null)
        {
            var sub = ImGui.CalcTextSize(subLabel);
            dl.AddText(new System.Numerics.Vector2(pos.X + (w - sub.X) / 2, pos.Y + 22), text, subLabel);
        }
        else if (unlocked)
        {
            // Render a small checkmark via two line segments — the default ImGui font lacks
            // glyphs for unicode '✓'.
            var cx = pos.X + w / 2f;
            var cy = pos.Y + 30f;
            dl.AddLine(new System.Numerics.Vector2(cx - 6, cy - 1), new System.Numerics.Vector2(cx - 1, cy + 5), text, 2f);
            dl.AddLine(new System.Numerics.Vector2(cx - 1, cy + 5), new System.Numerics.Vector2(cx + 7, cy - 5), text, 2f);
        }
    }

    private void DrawTagChips(HashSet<string> tags)
    {
        bool first = true;
        foreach (var t in tags.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            if (!first) ImGui.SameLine();
            first = false;
            var label = FriendlyTagLabel(t);
            ImGui.PushStyleColor(ImGuiCol.Button, 0x4060A085u);
            ImGui.Button($"{label}##chip-{t}");
            ImGui.PopStyleColor();
        }
    }

    private void DrawProcessCards(HashSet<string> done)
    {
        var defs = processes.All.OrderBy(p => p.Code, StringComparer.Ordinal).ToList();
        if (defs.Count == 0)
        {
            ImGui.TextDisabled(Lang.Get("almanaccodex:detail-no-processes-registered"));
            return;
        }
        const float cardW = 150f;
        const float cardH = 72f;
        var avail = ImGui.GetContentRegionAvail();
        int cols = System.Math.Max(1, (int)(avail.X / (cardW + 8)));
        int i = 0;
        foreach (var def in defs)
        {
            if (i % cols != 0) ImGui.SameLine();
            DrawProcessCard(def, done.Contains(def.Code), cardW, cardH);
            i++;
        }
    }

    private void DrawProcessCard(ProcessDefinition def, bool isDone, float w, float h)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new System.Numerics.Vector2(w, h));
        var dl = ImGui.GetWindowDrawList();
        uint border = isDone ? 0xFF8FB9A8u : 0x80555555u;
        uint fill = isDone ? 0x3060A085u : 0x40202020u;
        uint titleColor = isDone ? 0xFFE7DBC0u : 0xFFAAAAAAu;
        uint badgeColor = isDone ? 0xFF8FB9A8u : 0x80888888u;
        uint hintColor = 0x80AAAAAAu;
        dl.AddRectFilled(pos, new System.Numerics.Vector2(pos.X + w, pos.Y + h), fill, 4f);
        dl.AddRect(pos, new System.Numerics.Vector2(pos.X + w, pos.Y + h), border, 4f, ImDrawFlags.None, 1.5f);

        var title = FriendlyProcessLabel(def.Code);
        dl.AddText(new System.Numerics.Vector2(pos.X + 8, pos.Y + 6), titleColor, title);
        var badge = isDone ? Lang.Get("almanaccodex:process-done") : Lang.Get("almanaccodex:process-untried");
        var badgeSize = ImGui.CalcTextSize(badge);
        dl.AddText(new System.Numerics.Vector2(pos.X + w - badgeSize.X - 8, pos.Y + 6), badgeColor, badge);

        // Outcome / hint slots — populated in pass 2 (data extension). Placeholder for now.
        dl.AddText(new System.Numerics.Vector2(pos.X + 8, pos.Y + 28), hintColor, "-> ???");
        var hint = Lang.GetIfExists("EN", "almanaccodex:process-hint-" + def.Code) ?? Lang.Get("almanaccodex:detail-process-hint-placeholder");
        dl.AddText(new System.Numerics.Vector2(pos.X + 8, pos.Y + 50), hintColor, hint);
    }

    private const float DetailIconSize = 64f;
    private const float DetailIconPad = 12f;

    private void DrawDetailIcon(AssetLocation code, int colorArgb)
    {
        // Reserve a padded container, draw a bordered backdrop, then queue the icon centered
        // within that padding. Gives the icon visual breathing room separate from the meta
        // text on its right.
        var pad = DetailIconPad;
        var containerSize = DetailIconSize + pad * 2;
        var topLeft = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new System.Numerics.Vector2(containerSize, containerSize));

        var dl = ImGui.GetWindowDrawList();
        var min = topLeft;
        var max = new System.Numerics.Vector2(topLeft.X + containerSize, topLeft.Y + containerSize);
        dl.AddRectFilled(min, max, 0x40202020u, 4f);
        dl.AddRect(min, max, 0x80555555u, 4f, ImDrawFlags.None, 1.5f);

        var stack = ResolveStack(code);
        if (stack != null)
        {
            iconOverlay.Requests.Add(new IconRenderRequest(stack, topLeft.X + pad, topLeft.Y + pad, DetailIconSize, colorArgb));
        }
    }

    private static string FriendlyTagLabel(string raw)
    {
        // "almanac-medicinal" -> "Medicinal"
        var idx = raw.IndexOf('-');
        var part = idx >= 0 ? raw.Substring(idx + 1) : raw;
        if (part.Length == 0) return raw;
        return char.ToUpperInvariant(part[0]) + part.Substring(1);
    }

    private static string FriendlyProcessLabel(string raw)
    {
        if (raw.Length == 0) return raw;
        return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
    }

    private static readonly HashSet<string> OrientationSuffixes = new(StringComparer.Ordinal)
    {
        "north", "south", "east", "west", "up", "down", "horizontal", "vertical",
    };

    private static string GroupKey(AssetLocation code)
    {
        // Strip orientation/rotation suffixes so VS's per-direction variant blocks fold into
        // a single Almanac entry. The display-name path failed because GetName() returns the
        // raw code for sighted-only blocks.
        var path = code.Path;
        var parts = path.Split('-');
        if (parts.Length > 1 && OrientationSuffixes.Contains(parts[^1]))
        {
            path = string.Join("-", parts, 0, parts.Length - 1);
        }
        return code.Domain + ":" + path;
    }

    private List<GridRow> BuildGridRows(IPlayer player)
    {
        // Group variants by their orientation-stripped code. Stage = MAX, tags = UNION,
        // canonical CodeKey is whichever variant sorts first lexicographically.
        var groups = new Dictionary<string, GridGroupBuilder>();
        foreach (var e in entries.All)
        {
            var key = e.Code.ToShortString();
            var stack = ResolveStack(e.Code);
            var rawName = stack?.GetName() ?? key;

            string[] tags = Array.Empty<string>();
            if (stack != null)
            {
                tags = capi.CollectibleTagRegistry
                    .SlowEnumerateTagNames(stack.Collectible.Tags)
                    .ToArray();
            }
            var stage = store.GetStage(player, key);

            var groupKey = GroupKey(e.Code);
            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new GridGroupBuilder(rawName);
                groups[groupKey] = group;
            }
            group.OfferDisplayName(rawName);
            group.Add(key, stage, tags);
        }

        var rows = new List<GridRow>(groups.Count);
        var query = searchQuery.Trim();
        foreach (var group in groups.Values)
        {
            var aggregatedTags = group.AggregatedTags();
            if (activeTagFilter.Length > 0)
            {
                if (group.MaxStage == DiscoveryStage.Unknown) continue;
                if (!aggregatedTags.Any(t => string.Equals(t, activeTagFilter, StringComparison.OrdinalIgnoreCase))) continue;
            }
            // Search filter: match against name (visible to all) AND tags (only when revealed).
            // Silhouettes only match if their name contains the query — never tag-leaked.
            if (query.Length > 0)
            {
                bool nameMatch = group.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool tagMatch = group.MaxStage >= DiscoveryStage.Held
                    && aggregatedTags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (!nameMatch && !tagMatch) continue;
            }
            rows.Add(new GridRow(
                CodeKey: group.CanonicalCode,
                Name: group.Name,
                Stage: group.MaxStage,
                Tags: aggregatedTags,
                VariantCodes: group.AllCodes()));
        }

        rows.Sort((a, b) =>
        {
            int aDisc = a.Stage == DiscoveryStage.Unknown ? 1 : 0;
            int bDisc = b.Stage == DiscoveryStage.Unknown ? 1 : 0;
            if (aDisc != bDisc) return aDisc - bDisc;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return rows;
    }

    private ItemStack? ResolveStack(AssetLocation code)
    {
        var collectible = capi.World.GetBlock(code) as CollectibleObject
            ?? capi.World.GetItem(code) as CollectibleObject;
        return collectible == null ? null : new ItemStack(collectible);
    }

    private readonly record struct GridRow(string CodeKey, string Name, DiscoveryStage Stage, string[] Tags, string[] VariantCodes);

    private sealed class GridGroupBuilder
    {
        public string Name { get; private set; }
        public DiscoveryStage MaxStage { get; private set; } = DiscoveryStage.Unknown;
        public string CanonicalCode { get; private set; } = "";

        private readonly SortedSet<string> codes = new(StringComparer.Ordinal);
        private readonly HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);

        public GridGroupBuilder(string name) { Name = name; }

        /// <summary>
        /// Updates the display name, preferring a "clean" localized name over the raw
        /// code fallback that GetName() returns for some variants. Heuristic: if the new
        /// name does not start with the domain prefix and is shorter than current, use it.
        /// </summary>
        public void OfferDisplayName(string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) return;
            bool currentLooksLikeCode = Name.Contains(':') || Name.StartsWith("block-") || Name.StartsWith("item-");
            bool candidateLooksLikeCode = candidate.Contains(':') || candidate.StartsWith("block-") || candidate.StartsWith("item-");
            if (currentLooksLikeCode && !candidateLooksLikeCode) Name = candidate;
            else if (!candidateLooksLikeCode && candidate.Length < Name.Length) Name = candidate;
        }

        public void Add(string code, DiscoveryStage stage, string[] tagsForThisVariant)
        {
            codes.Add(code);
            CanonicalCode = codes.Min!;
            if (stage > MaxStage) MaxStage = stage;
            foreach (var t in tagsForThisVariant) tags.Add(t);
        }

        public string[] AggregatedTags()
        {
            var arr = new string[tags.Count];
            tags.CopyTo(arr);
            Array.Sort(arr, StringComparer.OrdinalIgnoreCase);
            return arr;
        }

        public string[] AllCodes() => codes.ToArray();
    }
}
