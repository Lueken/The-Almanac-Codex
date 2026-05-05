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

    // Shared with vanilla handbook: setting "noHandbookPause" — false (default) = pause game, true = don't pause.
    private const string PauseSettingKey = "noHandbookPause";

    private string activeTagFilter = "";
    private string? selectedEntryCode;
    private bool isOpen;

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
    }

    public AlmanacDialog(ICoreClientAPI capi, AlmanacEntryRegistry entries, DiscoveryStore store)
        : base(capi, Lang.Get("almanaccodex:dialog-title"), WindowIdValue, includeTitleIntoId: false,
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;

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
        int total = entries.Count;
        int discovered = 0;
        foreach (var e in entries.All)
        {
            if (store.GetStage(player, e.Code.ToShortString()) != DiscoveryStage.Unknown) discovered++;
        }
        ImGui.TextUnformatted(Lang.Get("almanaccodex:dialog-counter", discovered, total));

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
            var label = Lang.Get("almanaccodex:filter-" + tag);
            if (ImGui.Selectable(label, activeTagFilter == tag))
            {
                activeTagFilter = tag;
            }
        }
    }

    private void DrawGrid()
    {
        var player = capi.World.Player;
        var rows = BuildGridRows(player);

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(Lang.Get("almanaccodex:grid-empty"));
            return;
        }

        // Selectables in a multi-column table approximate a grid until the icon bridge lands.
        const int cols = 4;
        if (ImGui.BeginTable("almanac.grid.table", cols, ImGuiTableFlags.SizingStretchSame))
        {
            int i = 0;
            foreach (var row in rows)
            {
                if (i % cols == 0) ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var label = $"{row.Name}##{row.CodeKey}";
                if (ImGui.Selectable(label, false, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0, 38)))
                {
                    selectedEntryCode = row.CodeKey;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted(row.Name);
                    ImGui.TextDisabled(row.CodeKey);
                    ImGui.TextDisabled(Lang.Get("almanaccodex:tooltip-stage-" + row.Stage.ToString().ToLowerInvariant()));
                    if (row.Tags.Length > 0)
                    {
                        ImGui.TextDisabled(string.Join(", ", row.Tags));
                    }
                    ImGui.EndTooltip();
                }
                i++;
            }
            ImGui.EndTable();
        }
    }

    private void DrawDetailPanel(string codeKey)
    {
        var entry = entries.All.FirstOrDefault(e => e.Code.ToShortString() == codeKey);
        if (entry == null)
        {
            selectedEntryCode = null;
            return;
        }

        if (ImGui.Button(Lang.Get("almanaccodex:detail-back")))
        {
            selectedEntryCode = null;
            return;
        }

        var player = capi.World.Player;
        var stage = store.GetStage(player, codeKey);
        var displayStack = ResolveStack(entry.Code);
        var name = displayStack?.GetName() ?? codeKey;

        ImGui.SameLine();
        ImGui.TextUnformatted(name);
        ImGui.TextDisabled(Lang.Get("almanaccodex:tooltip-stage-" + stage.ToString().ToLowerInvariant()));
        ImGui.Separator();

        ImGui.TextUnformatted(Lang.Get("almanaccodex:detail-tags-heading"));
        if (stage >= DiscoveryStage.Held && displayStack != null)
        {
            var tagNames = capi.CollectibleTagRegistry
                .SlowEnumerateTagNames(displayStack.Collectible.Tags)
                .ToArray();
            ImGui.TextWrapped(tagNames.Length > 0 ? string.Join(", ", tagNames) : Lang.Get("almanaccodex:detail-tags-none"));
        }
        else
        {
            ImGui.TextDisabled("???");
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Lang.Get("almanaccodex:detail-processes-heading"));
        if (stage >= DiscoveryStage.Held)
        {
            var processes = store.ProcessesUnlocked(player, codeKey).ToArray();
            if (processes.Length == 0)
            {
                ImGui.TextDisabled(Lang.Get("almanaccodex:detail-processes-empty"));
            }
            else
            {
                foreach (var p in processes) ImGui.BulletText(p);
            }
        }
        else
        {
            ImGui.TextDisabled("???");
        }
    }

    private List<GridRow> BuildGridRows(IPlayer player)
    {
        var rows = new List<GridRow>();
        foreach (var e in entries.All)
        {
            var key = e.Code.ToShortString();
            var stage = store.GetStage(player, key);
            if (stage == DiscoveryStage.Unknown) continue;

            var stack = ResolveStack(e.Code);
            string[] tags = Array.Empty<string>();
            if (stack != null)
            {
                tags = capi.CollectibleTagRegistry
                    .SlowEnumerateTagNames(stack.Collectible.Tags)
                    .ToArray();
            }

            if (activeTagFilter.Length > 0)
            {
                if (stage < DiscoveryStage.Held) continue;
                if (!tags.Any(t => string.Equals(t, activeTagFilter, StringComparison.OrdinalIgnoreCase))) continue;
            }

            rows.Add(new GridRow(
                CodeKey: key,
                Name: stack?.GetName() ?? key,
                Stage: stage,
                Tags: tags));
        }
        rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private ItemStack? ResolveStack(AssetLocation code)
    {
        var collectible = capi.World.GetBlock(code) as CollectibleObject
            ?? capi.World.GetItem(code) as CollectibleObject;
        return collectible == null ? null : new ItemStack(collectible);
    }

    private readonly record struct GridRow(string CodeKey, string Name, DiscoveryStage Stage, string[] Tags);
}
