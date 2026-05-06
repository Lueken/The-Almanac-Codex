using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AlmanacCodex.Gui.Theme;
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

    private const string PauseSettingKey = "noHandbookPause";
    private const string SortSettingKey = "almanaccodex.sort";

    private string activeTagFilter = "";
    private string? selectedEntryCode;
    private bool isOpen;
    private float outerDialogTop;
    private float outerDialogBottom;
    private string searchQuery = "";
    private SortMode sortMode;

    // Stable catalog: every group key gets a 1-based index for the "№ NNN / TOTAL" displays.
    // Built lazily once the registry has stabilised.
    private Dictionary<string, int>? entryNumberCache;
    private int catalogTotal;

    private enum SortMode { Number, Name, Recency }

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
            capi.ModLoader.GetModSystem<VSImGui.ImGuiModSystem>()?.Show();
            ApplyPauseFromSetting();
            if (!iconOverlay.IsOpened()) iconOverlay.TryOpen();
        }
    }

    protected override bool OnClose()
    {
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
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;
        this.processes = processes;
        this.iconOverlay = new IconOverlayDialog(capi);

        sortMode = LoadSortMode();

        SetGrabMouseTrue();
    }

    private SortMode LoadSortMode()
    {
        var raw = capi.Settings.String[SortSettingKey];
        if (!string.IsNullOrEmpty(raw) && System.Enum.TryParse<SortMode>(raw, ignoreCase: true, out var mode))
            return mode;
        return SortMode.Number;
    }

    private void SaveSortMode()
    {
        capi.Settings.String[SortSettingKey] = sortMode.ToString();
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
        CodexStyle.Push();
        try
        {
            ImGui.SetWindowSize(new Vector2(880, 580), ImGuiCond.FirstUseEver);

            if (ImGui.IsKeyPressed(ImGuiKey.Escape, repeat: false))
            {
                Close();
                HandleClosed();
                return false;
            }

            iconOverlay.Requests.Clear();
            iconOverlay.CellLabels.Clear();
            // Suppress the icon overlay whenever an ImGui popup is open (sort dropdown, etc.)
            // so the popup stays the most-forward element instead of being overdrawn by the
            // post-ImGui icon + label pass.
            iconOverlay.SuppressOverlay = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

            var outerPos = ImGui.GetWindowPos();
            var outerSize = ImGui.GetWindowSize();
            outerDialogTop = outerPos.Y;
            outerDialogBottom = outerPos.Y + outerSize.Y;
            iconOverlay.ClipBounds = new ClipRect(outerPos.X, outerPos.Y, outerSize.X, outerSize.Y);

            DrawTopBar();
            // Top bar consumes ~64 + a hairline + 8px of breathing room. Place subsequent
            // flow content below it.
            ImGui.SetCursorPos(new Vector2(CodexTheme.WindowPadding, CodexTheme.TopBarHeight + 12));

            if (selectedEntryCode != null)
            {
                DrawDetailPanel(selectedEntryCode);
                return Opened;
            }

            var avail = ImGui.GetContentRegionAvail();
            if (ImGui.BeginChild("almanac.sidebar", new Vector2(CodexTheme.SidebarWidth, avail.Y), border: false))
            {
                DrawSidebar();
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (ImGui.BeginChild("almanac.grid", new Vector2(0, avail.Y)))
            {
                DrawGrid();
            }
            ImGui.EndChild();

            return Opened;
        }
        finally
        {
            CodexStyle.Pop();
        }
    }

    // ── Top bar ──────────────────────────────────────────────────────────────
    private void DrawTopBar()
    {
        var player = capi.World.Player;

        var groupStages = new Dictionary<string, DiscoveryStage>();
        foreach (var e in entries.All)
        {
            var gk = AlmanacEntry.GetGroupKey(e.Code);
            var stage = store.GetStage(player, e.Code.ToShortString());
            if (!groupStages.TryGetValue(gk, out var prev) || stage > prev) groupStages[gk] = stage;
        }
        int total = groupStages.Count;
        int discovered = 0;
        foreach (var s in groupStages.Values) if (s != DiscoveryStage.Unknown) discovered++;

        var winPos = ImGui.GetWindowPos();
        var winSize = ImGui.GetWindowSize();
        float barH = CodexTheme.TopBarHeight;
        var dl = ImGui.GetWindowDrawList();

        // Raised parchment band (rounded only on top corners to follow the window radius)
        dl.AddRectFilled(
            winPos,
            new Vector2(winPos.X + winSize.X, winPos.Y + barH),
            CodexTheme.U(CodexTheme.ParchmentRaised),
            CodexTheme.RadiusWindow,
            ImDrawFlags.RoundCornersTop);

        // Hairline divider at bottom of bar
        CodexDraw.Hairline(dl, winPos.X, winPos.Y + barH, winPos.X + winSize.X, winPos.Y + barH);

        // ── Left cluster ───────────────────────────────────────────────────
        if (selectedEntryCode != null)
        {
            // Detail mode → Back button. Render it via ImGui flow at an explicit screen pos.
            ImGui.SetCursorScreenPos(new Vector2(winPos.X + CodexTheme.WindowPadding, winPos.Y + 18));
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
            if (ImGui.Button("« " + Lang.Get("almanaccodex:detail-back"))) selectedEntryCode = null;
            ImGui.PopFont();
        }
        else
        {
            // Index mode → "THE ALMANAC · CODEX" overline + "Forager's index" display title.
            CodexDraw.DrawOverline(
                Lang.Get("almanaccodex:overline-codex"),
                new Vector2(winPos.X + CodexTheme.WindowPadding, winPos.Y + 18));

            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontDisplay));
            dl.AddText(
                new Vector2(winPos.X + CodexTheme.WindowPadding, winPos.Y + 32),
                CodexTheme.U(CodexTheme.InkPrimary),
                Lang.Get("almanaccodex:title-foragers-index"));
            ImGui.PopFont();
        }

        // ── Center: search box (index mode only) ────────────────────────────
        if (selectedEntryCode == null)
        {
            const float searchW = 220f;
            float searchX = winPos.X + (winSize.X - searchW) / 2f;
            ImGui.SetCursorScreenPos(new Vector2(searchX, winPos.Y + 24));
            ImGui.SetNextItemWidth(searchW);
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
            ImGui.InputTextWithHint("##almanac.search",
                Lang.Get("almanaccodex:search-hint"), ref searchQuery, 64);
            ImGui.PopFont();
            if (searchQuery.Length > 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("x##almanac.search.clear")) searchQuery = "";
            }
        }

        // ── Right: discovery counter ────────────────────────────────────────
        const float counterW = 124f;
        float counterX = winPos.X + winSize.X - counterW - CodexTheme.WindowPadding;
        CodexDraw.DrawOverline(
            Lang.Get("almanaccodex:overline-discovery"),
            new Vector2(counterX, winPos.Y + 14));

        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontHeading));
        dl.AddText(new Vector2(counterX, winPos.Y + 28),
            CodexTheme.U(CodexTheme.GoldAccent),
            $"{discovered} / {total}");
        ImGui.PopFont();

        // Slim progress bar
        float pY = winPos.Y + 50;
        float pH = 6f;
        var pMin = new Vector2(counterX, pY);
        var pMax = new Vector2(counterX + counterW, pY + pH);
        dl.AddRectFilled(pMin, pMax, CodexTheme.U(CodexTheme.InsetBg), 3f);
        dl.AddRect(pMin, pMax, CodexTheme.U(CodexTheme.BorderHairline), 3f, ImDrawFlags.None, CodexTheme.StrokeHairline);
        if (total > 0 && discovered > 0)
        {
            float fillRatio = MathF.Min(1f, (float)discovered / total);
            float fillW = (counterW - 4) * fillRatio;
            dl.AddRectFilled(
                new Vector2(pMin.X + 2, pMin.Y + 2),
                new Vector2(pMin.X + 2 + fillW, pMin.Y + pH - 2),
                CodexTheme.U(CodexTheme.GoldAccent),
                1f);
        }
    }

    // ── Sidebar (categories + legend) ────────────────────────────────────────
    private void DrawSidebar()
    {
        var player = capi.World.Player;

        // Build per-tag counts (only over entries the player has Held+, so unsighted items don't
        // leak tag data).
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries.All)
        {
            var stage = store.GetStage(player, e.Code.ToShortString());
            if (stage < DiscoveryStage.Held) continue;
            var stack = ResolveStack(e.Code);
            if (stack == null) continue;
            foreach (var t in capi.CollectibleTagRegistry.SlowEnumerateTagNames(stack.Collectible.Tags))
            {
                tagCounts.TryGetValue(t, out var c);
                tagCounts[t] = c + 1;
            }
        }

        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        float colX = cursor.X + 8;
        float y = cursor.Y;

        CodexDraw.DrawOverline(Lang.Get("almanaccodex:overline-categories"), new Vector2(colX, y));
        y += 22;

        // "All" pill — always shown, gold-bordered when active.
        DrawCategoryRow(dl, colX, y, Lang.Get("almanaccodex:filter-all"), entries.Count, activeTagFilter == "", isPill: true,
            onClick: () => activeTagFilter = "");
        y += 30;

        ImGui.SetCursorScreenPos(new Vector2(colX, y - 4));
        // Tag rows
        foreach (var tag in TagFilters.All)
        {
            var label = Lang.Get("almanaccodex:filter-" + tag.Slug);
            tagCounts.TryGetValue(tag.Code, out var count);
            bool active = activeTagFilter == tag.Code;
            DrawCategoryRow(dl, colX, y, label, count, active, isPill: active,
                onClick: () => activeTagFilter = active ? "" : tag.Code);
            y += 20;
        }

        // ── Legend at bottom ────────────────────────────────────────────────
        y += 16;
        CodexDraw.DrawOverline(Lang.Get("almanaccodex:overline-legend"), new Vector2(colX, y));
        y += 16;

        DrawLegendRow(dl, colX, y, DiscoveryStage.Sighted,  Lang.Get("almanaccodex:legend-sighted"));    y += 22;
        DrawLegendRow(dl, colX, y, DiscoveryStage.Held,     Lang.Get("almanaccodex:legend-held"));       y += 22;
        DrawLegendRow(dl, colX, y, DiscoveryStage.Processed, Lang.Get("almanaccodex:legend-processed")); y += 22;
        DrawLegendRow(dl, colX, y, DiscoveryStage.Unknown,  Lang.Get("almanaccodex:legend-unsighted"));

        // Reserve overall sidebar space so EndChild doesn't collapse it.
        ImGui.Dummy(new Vector2(CodexTheme.SidebarWidth - 16, y - cursor.Y + 24));
    }

    private void DrawCategoryRow(ImDrawListPtr dl, float x, float y, string label, int count, bool active, bool isPill, Action onClick)
    {
        const float rowW = 124f;
        const float rowH = 22f;
        var min = new Vector2(x, y);
        var max = new Vector2(x + rowW, y + rowH);

        // Click hit-test via invisible button at the same screen position.
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"sidebar.row.{label}", new Vector2(rowW, rowH));
        bool hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked()) onClick();

        if (isPill)
        {
            dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.ChipBgWarm), CodexTheme.RadiusVariantTab);
            dl.AddRect(min, max, CodexTheme.U(CodexTheme.GoldAccent), CodexTheme.RadiusVariantTab,
                ImDrawFlags.None, CodexTheme.StrokeHairline);
        }
        else if (hovered)
        {
            dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.WithAlpha(CodexTheme.ChipBgWarm, 0.4f)),
                CodexTheme.RadiusVariantTab);
        }

        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
        var labelColor = active ? CodexTheme.U(CodexTheme.InkPrimary) : CodexTheme.U(CodexTheme.InkSecondary);
        var countColor = active ? CodexTheme.U(CodexTheme.InkSecondary) : CodexTheme.U(CodexTheme.InkMuted);
        dl.AddText(new Vector2(x + 12, y + 4), labelColor, label);

        var countStr = count.ToString();
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontOverline));
        var ts = ImGui.CalcTextSize(countStr);
        dl.AddText(new Vector2(x + rowW - ts.X - 12, y + 6), countColor, countStr);
        ImGui.PopFont();
        ImGui.PopFont();
    }

    private void DrawLegendRow(ImDrawListPtr dl, float x, float y, DiscoveryStage stage, string label)
    {
        // Render the stage-dot indicator + label.
        var dotsCenter = new Vector2(x + 8, y + 6);
        if (stage == DiscoveryStage.Unknown)
        {
            // Single hollow dot
            dl.AddCircleFilled(dotsCenter, CodexTheme.StageDotRadius + 1, CodexTheme.U(CodexTheme.ChipBgWarm));
            dl.AddCircle(dotsCenter, CodexTheme.StageDotRadius + 1, CodexTheme.U(CodexTheme.BorderHairline),
                0, CodexTheme.StageDotEmptyStroke);
        }
        else
        {
            CodexDraw.StageDots(dl, dotsCenter, stage);
        }

        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
        var color = stage == DiscoveryStage.Unknown
            ? CodexTheme.U(CodexTheme.InkMuted)
            : CodexTheme.U(CodexTheme.InkSecondary);
        dl.AddText(new Vector2(x + 32, y - 2), color, label);
        ImGui.PopFont();
    }

    // ── Grid ─────────────────────────────────────────────────────────────────
    private void DrawGrid()
    {
        var player = capi.World.Player;

        // Sort dropdown — sits above the section headers, left side.
        DrawSortControl();

        var rows = BuildGridRows(player);

        if (rows.Count == 0)
        {
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
            ImGui.TextDisabled(Lang.Get("almanaccodex:grid-empty"));
            ImGui.PopFont();
            return;
        }

        var gridWinPos = ImGui.GetWindowPos();
        var gridWinSize = ImGui.GetWindowSize();
        var gridTop = gridWinPos.Y;
        var gridBottom = gridWinPos.Y + gridWinSize.Y;

        var avail = ImGui.GetContentRegionAvail();
        int cols = Math.Max(1, (int)((avail.X + CodexTheme.GridCellGap) / CodexTheme.GridCellStride));
        var dl = ImGui.GetWindowDrawList();

        // Pre-count discovered vs undiscovered for the section header counts.
        int discoveredCount = rows.Count(r => r.Stage != DiscoveryStage.Unknown);
        int undiscoveredCount = rows.Count - discoveredCount;

        bool renderedDiscoveredHeader = discoveredCount == 0;
        bool renderedUndiscoveredHeader = undiscoveredCount == 0;

        int colInRow = 0;
        foreach (var row in rows)
        {
            if (!renderedDiscoveredHeader && row.Stage != DiscoveryStage.Unknown)
            {
                DrawGridSectionHeader(Lang.Get("almanaccodex:section-discovered-count", discoveredCount));
                renderedDiscoveredHeader = true;
                colInRow = 0;
            }
            if (!renderedUndiscoveredHeader && row.Stage == DiscoveryStage.Unknown)
            {
                DrawGridSectionHeader(Lang.Get("almanaccodex:section-undiscovered-count", undiscoveredCount));
                renderedUndiscoveredHeader = true;
                colInRow = 0;
            }

            if (colInRow > 0 && colInRow % cols == 0)
            {
                // Wrap to a new row.
            }
            else if (colInRow > 0)
            {
                ImGui.SameLine(0, CodexTheme.GridCellGap);
            }

            DrawCell(dl, row, gridTop, gridBottom);
            colInRow++;
        }
    }

    private void DrawSortControl()
    {
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
        ImGui.TextUnformatted(Lang.Get("almanaccodex:sort-label"));
        ImGui.SameLine(0, 6);

        ImGui.SetNextItemWidth(140);
        int currentIdx = (int)sortMode;
        var options = new[]
        {
            Lang.Get("almanaccodex:sort-mode-number"),
            Lang.Get("almanaccodex:sort-mode-name"),
            Lang.Get("almanaccodex:sort-mode-recency"),
        };
        if (ImGui.Combo("##almanac.sort", ref currentIdx, options, options.Length))
        {
            sortMode = (SortMode)currentIdx;
            SaveSortMode();
        }
        ImGui.PopFont();
        ImGui.Spacing();
    }

    private void DrawGridSectionHeader(string text)
    {
        ImGui.Spacing();
        var pos = ImGui.GetCursorScreenPos();
        CodexDraw.DrawOverline(text, pos);
        var dl = ImGui.GetWindowDrawList();
        var avail = ImGui.GetContentRegionAvail();
        // Hairline divider underneath the label
        CodexDraw.Hairline(dl, pos.X, pos.Y + 16, pos.X + avail.X, pos.Y + 16);
        ImGui.Dummy(new Vector2(avail.X, 22));
    }

    private void DrawCell(ImDrawListPtr dl, GridRow row, float gridTop, float gridBottom)
    {
        float w = CodexTheme.GridCellW;
        float h = CodexTheme.GridCellH;

        var topLeft = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton($"cell##{row.CodeKey}", new Vector2(w, h));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();
        var bottomRight = new Vector2(topLeft.X + w, topLeft.Y + h);

        bool visible = ImGui.IsItemVisible()
            && bottomRight.Y <= gridBottom
            && topLeft.Y >= gridTop;

        if (!visible) return;

        // Cell chrome
        bool isSelected = selectedEntryCode == row.CodeKey;
        var bg = CodexTheme.U(CodexTheme.InsetBg);
        var border = CodexTheme.U(hovered || isSelected ? CodexTheme.GoldAccent : CodexTheme.BorderHairline);
        float borderStroke = (hovered || isSelected) ? CodexTheme.StrokeStandard : CodexTheme.StrokeHairline;
        dl.AddRectFilled(topLeft, bottomRight, bg, CodexTheme.RadiusFrame);
        dl.AddRect(topLeft, bottomRight, border, CodexTheme.RadiusFrame, ImDrawFlags.None, borderStroke);

        // Entry number chip in the top-LEFT corner — routed through the icon overlay so it
        // renders AFTER the icon (post-ImGui pass via VS's native pipeline). Rendered with bg
        // + border so the digits stay legible regardless of what icon sits behind them.
        int entryNum = GetEntryNumber(AlmanacEntry.GetGroupKey(new AssetLocation(row.CodeKey)));
        // Y is negative because Cairo's GenTextTexture pads the texture above the visible
        // bg-rect with the font's full ascender headroom. Empirically at 11pt Georgia that's
        // ~10-12px of dead space; offsetting by -6 anchors the visible chip top close to
        // the cell border.
        iconOverlay.CellLabels.Add(new CellLabelRequest(
            entryNum.ToString("D3"),
            topLeft.X + 8,
            topLeft.Y - 15,
            hovered || isSelected));

        // Icon — vertically centered now that the name has moved to the tooltip.
        // Slightly larger (40px) to fill the freed space; offset just above center to
        // balance against the entry number at top and stage dots at bottom.
        var stack = ResolveStack(new AssetLocation(row.CodeKey));
        if (stack != null)
        {
            float iconSize = 40f;
            float iconLeft = topLeft.X + (w - iconSize) / 2f;
            float iconTop = topLeft.Y + 18f;
            int color = row.Stage == DiscoveryStage.Unknown ? IconArgbSilhouette : IconArgbNormal;
            iconOverlay.Requests.Add(new IconRenderRequest(stack, iconLeft, iconTop, iconSize, color));
        }

        // Stage dots at bottom (3 dots, stride 9 → cluster width = 18)
        var dotsCenter = new Vector2(topLeft.X + w / 2f - CodexTheme.StageDotStride, topLeft.Y + h - 10);
        if (row.Stage == DiscoveryStage.Unknown)
        {
            // Show three hollow dots
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector2(dotsCenter.X + i * CodexTheme.StageDotStride, dotsCenter.Y);
                dl.AddCircleFilled(c, CodexTheme.StageDotRadius, CodexTheme.U(CodexTheme.ChipBgWarm));
                dl.AddCircle(c, CodexTheme.StageDotRadius, CodexTheme.U(CodexTheme.BorderHairline),
                    0, CodexTheme.StageDotEmptyStroke);
            }
        }
        else
        {
            CodexDraw.StageDots(dl, dotsCenter, row.Stage);
        }

        if (clicked) selectedEntryCode = row.CodeKey;

        if (hovered)
        {
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
    }

    private const int IconArgbNormal = unchecked((int)0xFFFFFFFFu);
    private const int IconArgbSilhouette = unchecked((int)0xFF202020u);

    // ── Detail panel ─────────────────────────────────────────────────────────
    private void DrawDetailPanel(string canonicalCodeKey)
    {
        var canonicalEntry = entries.All.FirstOrDefault(e => e.Code.ToShortString() == canonicalCodeKey);
        if (canonicalEntry == null)
        {
            selectedEntryCode = null;
            return;
        }
        var canonicalGroupKey = AlmanacEntry.GetGroupKey(canonicalEntry.Code);

        var player = capi.World.Player;
        var groupCodes = new List<string>();
        var groupStage = DiscoveryStage.Unknown;
        var aggregatedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var aggregatedProcesses = new HashSet<string>(StringComparer.Ordinal);
        var perVariantStages = new Dictionary<string, DiscoveryStage>();
        string displayName = canonicalCodeKey;
        bool foundCleanName = false;
        double? earliestSightedAt = null;
        AssetLocation? earliestSightedAtCode = null;

        foreach (var e in entries.All)
        {
            if (AlmanacEntry.GetGroupKey(e.Code) != canonicalGroupKey) continue;

            var stack = ResolveStack(e.Code);
            var thisName = stack?.GetName() ?? e.Code.ToShortString();
            bool looksLikeCode = thisName.Contains(':') || thisName.StartsWith("block-") || thisName.StartsWith("item-");
            if (!foundCleanName && !looksLikeCode) { displayName = thisName; foundCleanName = true; }
            else if (!foundCleanName) displayName = thisName;

            var thisKey = e.Code.ToShortString();
            groupCodes.Add(thisKey);
            var thisStage = store.GetStage(player, thisKey);
            perVariantStages[thisKey] = thisStage;
            if (thisStage > groupStage) groupStage = thisStage;

            if (thisStage >= DiscoveryStage.Held && stack != null)
            {
                foreach (var t in capi.CollectibleTagRegistry.SlowEnumerateTagNames(stack.Collectible.Tags))
                    aggregatedTags.Add(t);
            }
            foreach (var p in store.ProcessesUnlocked(player, thisKey)) aggregatedProcesses.Add(p);

            var sightedAt = store.GetSightedAt(player, thisKey);
            if (sightedAt.HasValue && (!earliestSightedAt.HasValue || sightedAt.Value < earliestSightedAt.Value))
            {
                earliestSightedAt = sightedAt;
                earliestSightedAtCode = e.Code;
            }
        }

        var dl = ImGui.GetWindowDrawList();
        var basePos = ImGui.GetCursorScreenPos();
        float panelLeft = basePos.X + CodexTheme.WindowPadding;
        float panelRight = basePos.X + ImGui.GetContentRegionAvail().X - CodexTheme.WindowPadding;
        float y = basePos.Y;

        // ── Header: № NNN / TOTAL + name + Latin/classification + stage seals (right) ─
        int entryNum = GetEntryNumber(canonicalGroupKey);
        int total = CatalogTotal();
        CodexDraw.DrawOverline(
            Lang.Get("almanaccodex:detail-entry-number", entryNum.ToString("D3"), total.ToString("D3")),
            new Vector2(panelLeft, y));
        y += 18;

        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontDetailName));
        var nameToShow = groupStage == DiscoveryStage.Unknown ? "???" : displayName;
        dl.AddText(new Vector2(panelLeft, y), CodexTheme.U(CodexTheme.InkPrimary), nameToShow);
        ImGui.PopFont();
        y += 30;

        // Latin name + classification line (only when discovered + data present)
        if (groupStage != DiscoveryStage.Unknown)
        {
            var latin = canonicalEntry.LatinName;
            string? classification = canonicalEntry.ClassificationKey != null && Lang.HasTranslation(canonicalEntry.ClassificationKey)
                ? Lang.Get(canonicalEntry.ClassificationKey)
                : null;
            if (latin != null || classification != null)
            {
                ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
                string line = latin ?? "";
                if (classification != null) line += (latin != null ? " · " : "") + classification;
                dl.AddText(new Vector2(panelLeft, y), CodexTheme.U(CodexTheme.InkSecondary), line);
                ImGui.PopFont();
            }
            y += 22;
        }
        else
        {
            // Match the discovered branch's bottom padding so the header divider always
            // lands below the stage seals (seals span basePos.Y+8 .. basePos.Y+72).
            y += 22;
        }

        // Stage seals on the right of the header
        DrawStageSealsAt(panelRight, basePos.Y + 8, groupStage, aggregatedProcesses.Count, processes.All.Count);

        // Hairline divider
        CodexDraw.Hairline(dl, panelLeft, y + 6, panelRight, y + 6);
        y += 18;

        if (groupStage == DiscoveryStage.Unknown)
        {
            // Compact undiscovered view: silhouette + body copy
            DrawDetailIconAt(canonicalEntry.Code, panelLeft, y, IconArgbSilhouette);
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
            dl.AddText(new Vector2(panelLeft + CodexTheme.DetailIconBoxSize + 24, y),
                CodexTheme.U(CodexTheme.InkSecondary),
                Lang.Get("almanaccodex:detail-undiscovered-body"));
            ImGui.PopFont();
            return;
        }

        // ── Body row: icon container (left) + meta block (right) ──────────
        DrawDetailIconAt(canonicalEntry.Code, panelLeft, y, IconArgbNormal);

        float metaX = panelLeft + CodexTheme.DetailIconBoxSize + 24;
        float metaY = y;
        int overlineLg = (int)CodexTheme.FontOverlineLg;

        // HABITAT
        CodexDraw.DrawOverline(Lang.Get("almanaccodex:overline-habitat"), new Vector2(metaX, metaY), fontSize: overlineLg);
        metaY += 20;
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLgPlus));
        string habitat = canonicalEntry.HabitatKey != null && Lang.HasTranslation(canonicalEntry.HabitatKey)
            ? Lang.Get(canonicalEntry.HabitatKey)
            : Lang.Get("almanaccodex:detail-habitat-placeholder");
        dl.AddText(new Vector2(metaX, metaY), CodexTheme.U(CodexTheme.InkPrimary), habitat);
        ImGui.PopFont();
        metaY += 28;

        // PROPERTIES
        CodexDraw.DrawOverline(Lang.Get("almanaccodex:detail-properties-heading"), new Vector2(metaX, metaY), fontSize: overlineLg);
        metaY += 20;
        if (aggregatedTags.Count == 0)
        {
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
            dl.AddText(new Vector2(metaX, metaY), CodexTheme.U(CodexTheme.InkMuted),
                Lang.Get("almanaccodex:detail-tags-none"));
            ImGui.PopFont();
        }
        else
        {
            DrawPropertyChips(dl, new Vector2(metaX, metaY), aggregatedTags);
        }
        metaY += CodexTheme.ChipHeight + 12;

        // VARIANTS · K of N — hidden for now. Orientation variants are mechanical, not biological,
        // and the row adds visual noise without conveying much. Re-enable when per-variant
        // metadata (e.g. distinct discovery time + place per direction) is worth surfacing.
        const bool ShowVariants = false;
        #pragma warning disable CS0162 // Unreachable code
        if (ShowVariants && groupCodes.Count > 1)
        {
            int knownCount = perVariantStages.Count(kv => kv.Value != DiscoveryStage.Unknown);
            CodexDraw.DrawOverline(
                Lang.Get("almanaccodex:overline-variants", knownCount, groupCodes.Count),
                new Vector2(metaX, metaY), fontSize: overlineLg);
            metaY += 20;

            float vx = metaX;
            foreach (var code in groupCodes.OrderBy(c => c, StringComparer.Ordinal))
            {
                var loc = new AssetLocation(code);
                var orientation = AlmanacEntry.GetOrientationSuffix(loc) ?? "·";
                bool known = perVariantStages.TryGetValue(code, out var st) && st != DiscoveryStage.Unknown;
                CodexDraw.DrawVariantTab(dl, new Vector2(vx, metaY), orientation, known);
                vx += CodexTheme.VariantTabW + 6;
            }
            metaY += CodexTheme.VariantTabH + 14;
        }
        #pragma warning restore CS0162

        // FIRST OBSERVED
        CodexDraw.DrawOverline(Lang.Get("almanaccodex:overline-first-observed"), new Vector2(metaX, metaY), fontSize: overlineLg);
        metaY += 20;
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
        string firstObserved = earliestSightedAt.HasValue
            ? Lang.Get("almanaccodex:detail-first-observed-day", (int)earliestSightedAt.Value)
            : Lang.Get("almanaccodex:detail-first-observed-unknown");
        if (earliestSightedAtCode != null)
        {
            var orient = AlmanacEntry.GetOrientationSuffix(earliestSightedAtCode);
            if (orient != null) firstObserved += " - " + orient;
        }
        dl.AddText(new Vector2(metaX, metaY), CodexTheme.U(CodexTheme.InkSecondary), firstObserved);
        ImGui.PopFont();

        // Skip past the icon-container row (icon is 180 tall, meta block may be taller)
        float iconBottom = y + CodexTheme.DetailIconBoxSize;
        float bodyBottom = MathF.Max(iconBottom, metaY + 24);

        // ── Description (full-width, wrapped) ─────────────────────────────
        string? description = canonicalEntry.DescriptionKey != null && Lang.HasTranslation(canonicalEntry.DescriptionKey)
            ? Lang.Get(canonicalEntry.DescriptionKey)
            : null;

        float sectionDivider = bodyBottom + 8;
        if (description != null)
        {
            CodexDraw.Hairline(dl, panelLeft, sectionDivider, panelRight, sectionDivider);

            var descLabelY = sectionDivider + 14;
            CodexDraw.DrawOverline(
                Lang.Get("almanaccodex:overline-description"),
                new Vector2(panelLeft, descLabelY), fontSize: overlineLg);

            var descBodyY = descLabelY + 22;
            ImGui.SetCursorScreenPos(new Vector2(panelLeft, descBodyY));
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
            ImGui.PushStyleColor(ImGuiCol.Text, CodexTheme.InkPrimary);
            // Wrap to the panel's right edge — convert screen X to window-local.
            var winPos = ImGui.GetWindowPos();
            ImGui.PushTextWrapPos(panelRight - winPos.X);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            ImGui.PopFont();

            sectionDivider = ImGui.GetCursorScreenPos().Y + 8;
        }

        // ── Processes section ─────────────────────────────────────────────
        CodexDraw.Hairline(dl, panelLeft, sectionDivider, panelRight, sectionDivider);
        var procY = sectionDivider + 16;
        CodexDraw.DrawOverline(
            Lang.Get("almanaccodex:detail-processes-heading-progress",
                aggregatedProcesses.Count, processes.All.Count),
            new Vector2(panelLeft, procY), fontSize: overlineLg);
        DrawProcessCardsAt(dl, panelLeft, procY + 22, panelRight - panelLeft, aggregatedProcesses);
    }

    // ── Stage seals (top-right of detail header) ─────────────────────────────
    private void DrawStageSealsAt(float rightX, float topY, DiscoveryStage stage, int processesDone, int processesTotal)
    {
        var dl = ImGui.GetWindowDrawList();
        // Three seals laid out right-to-left from rightX, with a small inset so the
        // rightmost seal doesn't kiss the dialog edge.
        float r = CodexTheme.SealRadius;
        float stride = CodexTheme.SealStride;
        float anchor = rightX - 8;
        float baseY = topY + r;

        // Processed (rightmost) — shorter "USED" label keeps the text inside the inner ring.
        var processedCenter = new Vector2(anchor - r, baseY);
        CodexDraw.DrawSeal(dl, processedCenter, "USED", processesDone > 0, $"{processesDone}/{processesTotal}");

        // Held (middle)
        var heldCenter = new Vector2(anchor - r - stride, baseY);
        CodexDraw.DrawSeal(dl, heldCenter, "HELD", stage >= DiscoveryStage.Held);

        // Sighted (leftmost) — "SEEN" matches the 4-char rhythm of HELD/USED for visual symmetry.
        var sightedCenter = new Vector2(anchor - r - stride * 2, baseY);
        CodexDraw.DrawSeal(dl, sightedCenter, "SEEN", stage >= DiscoveryStage.Sighted);
    }

    // ── Property chips (replaces ImGui.Button hack) ──────────────────────────
    private void DrawPropertyChips(ImDrawListPtr dl, Vector2 origin, HashSet<string> tags)
    {
        float x = origin.X;
        // Measure with the chip's render font (FontTitle, 13pt) so widths track the actual text size.
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
        foreach (var t in tags.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            var label = FriendlyTagLabel(t);
            var slug = TagSlug(t);
            var colors = CodexTheme.GetChipColors(slug);
            float w = MathF.Max(76f, ImGui.CalcTextSize(label).X + 28f);
            CodexDraw.DrawPillChip(dl, new Vector2(x, origin.Y), w, label, colors);
            x += w + 8;
        }
        ImGui.PopFont();
    }

    // ── Process cards ────────────────────────────────────────────────────────
    private void DrawProcessCardsAt(ImDrawListPtr dl, float x, float y, float availW, HashSet<string> done)
    {
        var defs = processes.All.OrderBy(p => p.Code, StringComparer.Ordinal).ToList();
        if (defs.Count == 0)
        {
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBody));
            dl.AddText(new Vector2(x, y), CodexTheme.U(CodexTheme.InkMuted),
                Lang.Get("almanaccodex:detail-no-processes-registered"));
            ImGui.PopFont();
            return;
        }

        float cardW = CodexTheme.ProcessCardW;
        float cardH = CodexTheme.ProcessCardH;
        float gap = CodexTheme.ProcessCardGap;
        int cols = Math.Max(1, (int)((availW + gap) / (cardW + gap)));

        for (int i = 0; i < defs.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            float cx = x + col * (cardW + gap);
            float cy = y + row * (cardH + gap);
            DrawProcessCard(dl, defs[i], done.Contains(defs[i].Code), new Vector2(cx, cy));
        }
    }

    private void DrawProcessCard(ImDrawListPtr dl, ProcessDefinition def, bool isDone, Vector2 pos)
    {
        float w = CodexTheme.ProcessCardW;
        float h = CodexTheme.ProcessCardH;
        float headerH = CodexTheme.ProcessCardHeader;
        var min = pos;
        var max = new Vector2(pos.X + w, pos.Y + h);
        var headerMax = new Vector2(pos.X + w, pos.Y + headerH);

        if (isDone)
        {
            dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.ProcessDoneBg), CodexTheme.RadiusFrame);
            // Header strip (only top-rounded)
            dl.AddRectFilled(min, headerMax, CodexTheme.U(CodexTheme.ProcessDoneHeader),
                CodexTheme.RadiusFrame, ImDrawFlags.RoundCornersTop);
            dl.AddRect(min, max, CodexTheme.U(CodexTheme.ProcessDoneBorder), CodexTheme.RadiusFrame,
                ImDrawFlags.None, CodexTheme.StrokeHairline);
        }
        else
        {
            CodexDraw.DashedRect(dl, min, max,
                CodexTheme.U(CodexTheme.ProcessUntriedBorder),
                CodexTheme.DashLength, CodexTheme.DashGap, CodexTheme.RadiusFrame);
        }

        var titleColor = isDone ? CodexTheme.U(CodexTheme.ProcessDoneTitle) : CodexTheme.U(CodexTheme.ProcessUntriedTitle);
        var outcomeColor = isDone ? CodexTheme.U(CodexTheme.ProcessDoneOutcome) : CodexTheme.U(CodexTheme.InkDisabled);
        var flavorColor = isDone ? CodexTheme.U(CodexTheme.ProcessDoneFlavor) : CodexTheme.U(CodexTheme.InkDisabled);
        var hintColor = isDone ? CodexTheme.U(CodexTheme.ProcessDoneHint) : CodexTheme.U(CodexTheme.ProcessUntriedHint);

        // Title (header strip)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
        var title = Lang.HasTranslation(def.DisplayKey) ? Lang.Get(def.DisplayKey) : FriendlyProcessLabel(def.Code);
        dl.AddText(new Vector2(pos.X + 12, pos.Y + 8), titleColor, title);
        ImGui.PopFont();

        // Status badge (right of header)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontOverline));
        var badge = isDone ? Lang.Get("almanaccodex:process-done") : Lang.Get("almanaccodex:process-untried");
        var badgeSize = ImGui.CalcTextSize(badge);
        dl.AddText(new Vector2(pos.X + w - badgeSize.X - 12, pos.Y + 10), titleColor, badge);
        ImGui.PopFont();

        // Outcome (large body line)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLgPlus));
        string outcomeText = ResolveOutcomeText(def, isDone);
        dl.AddText(new Vector2(pos.X + 12, pos.Y + headerH + 8), outcomeColor, outcomeText);
        ImGui.PopFont();

        // Flavor (italic-ish — Georgia italic isn't bundled separately, so we render in the
        // muted secondary color to evoke the italic role)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBodyLg));
        string flavor = (isDone && def.FlavorKey != null && Lang.HasTranslation(def.FlavorKey))
            ? Lang.Get(def.FlavorKey)
            : Lang.Get("almanaccodex:process-flavor-unknown");
        dl.AddText(new Vector2(pos.X + 12, pos.Y + headerH + 30), flavorColor, flavor);
        ImGui.PopFont();

        // Hint (small bottom line)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontBody));
        string hint = def.HintKey != null && Lang.HasTranslation(def.HintKey)
            ? Lang.Get(def.HintKey)
            : Lang.Get("almanaccodex:process-hint-unknown");
        dl.AddText(new Vector2(pos.X + 12, pos.Y + headerH + 50), hintColor, hint);
        ImGui.PopFont();
    }

    private string ResolveOutcomeText(ProcessDefinition def, bool isDone)
    {
        if (!isDone || def.OutcomeCode == null) return Lang.Get("almanaccodex:process-outcome-unknown");
        var outcomeStack = capi.World.GetBlock(def.OutcomeCode) is { } b ? new ItemStack(b)
            : capi.World.GetItem(def.OutcomeCode) is { } it ? new ItemStack(it, def.OutcomeQuantity)
            : null;
        var name = outcomeStack?.GetName() ?? def.OutcomeCode.ToShortString();
        return "→ " + (def.OutcomeQuantity > 1 ? $"{def.OutcomeQuantity}× " : "") + name;
    }

    // ── Detail icon container ────────────────────────────────────────────────
    private void DrawDetailIconAt(AssetLocation code, float x, float y, int colorArgb)
    {
        var dl = ImGui.GetWindowDrawList();
        float size = CodexTheme.DetailIconBoxSize;
        var min = new Vector2(x, y);
        var max = new Vector2(x + size, y + size);
        dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.InsetBg), CodexTheme.RadiusFrame);
        dl.AddRect(min, max, CodexTheme.U(CodexTheme.BorderHairline), CodexTheme.RadiusFrame,
            ImDrawFlags.None, CodexTheme.StrokeHairline);

        var stack = ResolveStack(code);
        if (stack != null)
        {
            // Icon shrunk from 100 -> 80 so VS's RenderItemstackToGui upward-perspective
            // overflow doesn't kiss the container's top border on tall plant silhouettes.
            float iconSize = 80f;
            float iconLeft = x + (size - iconSize) / 2f;
            float iconTop = y + (size - iconSize) / 2f;
            iconOverlay.Requests.Add(new IconRenderRequest(stack, iconLeft, iconTop, iconSize, colorArgb));
        }
    }

    // ── Helpers (data layer untouched from prior session) ────────────────────
    private static string FriendlyTagLabel(string raw)
    {
        var idx = raw.IndexOf('-');
        var part = idx >= 0 ? raw.Substring(idx + 1) : raw;
        if (part.Length == 0) return raw;
        return char.ToUpperInvariant(part[0]) + part.Substring(1);
    }

    private static string TagSlug(string raw)
    {
        // "almanac-medicinal" → "medicinal"
        var idx = raw.IndexOf('-');
        return idx >= 0 ? raw.Substring(idx + 1) : raw;
    }

    private static string FriendlyProcessLabel(string raw)
    {
        if (raw.Length == 0) return raw;
        return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
    }

    private int GetEntryNumber(string groupKey)
    {
        if (entryNumberCache == null)
        {
            var allGroups = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in entries.All) allGroups.Add(AlmanacEntry.GetGroupKey(e.Code));
            entryNumberCache = new Dictionary<string, int>(allGroups.Count);
            int idx = 1;
            foreach (var k in allGroups) entryNumberCache[k] = idx++;
            catalogTotal = entryNumberCache.Count;
        }
        return entryNumberCache.TryGetValue(groupKey, out var n) ? n : 0;
    }

    private int CatalogTotal()
    {
        if (entryNumberCache == null) GetEntryNumber("");
        return catalogTotal;
    }

    private List<GridRow> BuildGridRows(IPlayer player)
    {
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

            var groupKey = AlmanacEntry.GetGroupKey(e.Code);
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

        // Sort: section split (discovered before undiscovered) is preserved across all modes.
        // Within each section, the secondary comparator is selected via sortMode. Entries
        // without timestamps in Recency mode fall back to entry-number order.
        rows.Sort((a, b) =>
        {
            int aDisc = a.Stage == DiscoveryStage.Unknown ? 1 : 0;
            int bDisc = b.Stage == DiscoveryStage.Unknown ? 1 : 0;
            if (aDisc != bDisc) return aDisc - bDisc;

            switch (sortMode)
            {
                case SortMode.Name:
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                case SortMode.Recency:
                    var aTime = store.GetSightedAt(player, a.CodeKey);
                    var bTime = store.GetSightedAt(player, b.CodeKey);
                    if (aTime.HasValue && bTime.HasValue) return bTime.Value.CompareTo(aTime.Value); // newest first
                    if (aTime.HasValue) return -1;
                    if (bTime.HasValue) return 1;
                    goto case SortMode.Number; // fall through for entries without timestamps

                case SortMode.Number:
                default:
                    int aNum = GetEntryNumber(AlmanacEntry.GetGroupKey(new AssetLocation(a.CodeKey)));
                    int bNum = GetEntryNumber(AlmanacEntry.GetGroupKey(new AssetLocation(b.CodeKey)));
                    return aNum.CompareTo(bNum);
            }
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
