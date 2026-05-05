using System.Collections.Generic;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace AlmanacCodex.Handbook;

public class HandbookIntegration
{
    private readonly ICoreClientAPI capi;
    private readonly AlmanacEntryRegistry entries;
    private readonly DiscoveryStore store;
    private readonly List<CodexHandbookPage> ourPages = new();

    public HandbookIntegration(ICoreClientAPI capi, AlmanacEntryRegistry entries, DiscoveryStore store)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;

        var hbk = capi.ModLoader.GetModSystem<ModSystemSurvivalHandbook>();
        if (hbk == null)
        {
            CodexLogger.Warn(capi, "handbook-tab",
                "ModSystemSurvivalHandbook not found; skipping tab integration");
            return;
        }
        hbk.OnInitCustomPages += OnInitCustomPages;

        var langProbe = Lang.Get("handbook-category-almanac");
        CodexLogger.Info(capi, "handbook-tab",
            $"lang probe: 'handbook-category-almanac' -> '{langProbe}' (matches literal? {langProbe == "handbook-category-almanac"})");

        capi.Event.RegisterGameTickListener(_ => RefreshVisibility(), 1000);
    }

    private void OnInitCustomPages(List<GuiHandbookPage> pages)
    {
        ourPages.Clear();
        foreach (var entry in entries.All)
        {
            var page = new CodexHandbookPage(capi, entry, store);
            page.RefreshVisibility();
            pages.Add(page);
            ourPages.Add(page);
        }
        CodexLogger.Info(capi, "handbook-tab",
            $"injected {ourPages.Count} pages into Survival Handbook (category='{CodexHandbookPage.Category}')");
    }

    private void RefreshVisibility()
    {
        foreach (var p in ourPages)
        {
            p.RefreshVisibility();
        }
    }
}
