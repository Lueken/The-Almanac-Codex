using System.Collections.Generic;
using System.Reflection;
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
    private readonly ModSystemSurvivalHandbook? handbookMod;
    private readonly FieldInfo? dialogField;
    private CodexHandbookPage? launchpad;
    private string lastSeenCategory = "";
    private bool autoOpenedThisVisit;
    private bool firstFindLogged;

    public HandbookIntegration(ICoreClientAPI capi, AlmanacEntryRegistry entries, DiscoveryStore store)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;

        handbookMod = capi.ModLoader.GetModSystem<ModSystemSurvivalHandbook>();
        if (handbookMod == null)
        {
            CodexLogger.Warn(capi, "handbook-tab",
                "ModSystemSurvivalHandbook not found; skipping launchpad page");
            return;
        }
        handbookMod.OnInitCustomPages += OnInitCustomPages;

        // Vanilla stores its dialog as a private field on the modsystem. We grab it via reflection
        // because LoadedGuis lookup turned out to be unreliable timing-wise.
        dialogField = typeof(ModSystemSurvivalHandbook).GetField("dialog",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (dialogField == null)
        {
            CodexLogger.Warn(capi, "handbook-tab",
                "could not locate 'dialog' field on ModSystemSurvivalHandbook — auto-open disabled");
        }

        var langProbe = Lang.Get("handbook-category-almanac");
        CodexLogger.Info(capi, "handbook-tab",
            $"lang probe: 'handbook-category-almanac' -> '{langProbe}' (matches literal? {langProbe == "handbook-category-almanac"})");

        // Auto-open the launchpad page when the player clicks the "The Almanac" tab.
        capi.Event.RegisterGameTickListener(_ => CheckAutoOpen(), 100);
    }

    private void OnInitCustomPages(List<GuiHandbookPage> pages)
    {
        var cards = SubmodCards.Build(capi.ModLoader);
        launchpad = new CodexHandbookPage(capi, entries, store, cards);
        pages.Add(launchpad);
        CodexLogger.Info(capi, "handbook-tab",
            $"injected launchpad page (category='{CodexHandbookPage.Category}', submods={cards.Count})");
    }

    private long lastDiagnosticMs;

    private void CheckAutoOpen()
    {
        if (handbookMod == null || dialogField == null) return;

        var raw = dialogField.GetValue(handbookMod);
        var dialog = raw as GuiDialogHandbook;

        var nowMs = capi.ElapsedMilliseconds;
        if (nowMs - lastDiagnosticMs > 2000)
        {
            lastDiagnosticMs = nowMs;
            CodexLogger.Info(capi, "handbook-tab",
                $"tick: rawNull={raw == null} type={raw?.GetType().FullName ?? "<null>"} opened={dialog?.IsOpened() ?? false}");
        }

        if (dialog == null)
        {
            lastSeenCategory = "";
            autoOpenedThisVisit = false;
            return;
        }
        // Note: dialog.IsOpened() returns false even when the player has the handbook visible
        // (vanilla state-sync quirk). We skip that gate and rely on currentCatgoryCode + the
        // idempotency of OpenDetailPageFor (returns true with no side effects if already on the page).

        if (!firstFindLogged)
        {
            CodexLogger.Info(capi, "handbook-tab",
                $"located handbook dialog of type {dialog.GetType().FullName}");
            firstFindLogged = true;
        }

        var current = dialog.currentCatgoryCode ?? "";
        if (current != lastSeenCategory)
        {
            lastSeenCategory = current;
            CodexLogger.Info(capi, "handbook-tab", $"category transition -> '{current}'");
        }

        if (current != CodexHandbookPage.Category) return;

        // Re-trigger OpenDetailPageFor whenever our category is active AND browseHistory is
        // empty (vanilla clears it on tab-click-from-detail). OpenDetailPageFor is idempotent —
        // it returns true with no side effects when our page is already at the top of history.
        if (BrowseHistoryEmpty(dialog))
        {
            if (dialog.OpenDetailPageFor(CodexHandbookPage.PageId))
            {
                CodexLogger.Info(capi, "handbook-tab",
                    $"auto-opened launchpad detail page (category='{current}')");
            }
            else
            {
                CodexLogger.Warn(capi, "handbook-tab",
                    $"OpenDetailPageFor('{CodexHandbookPage.PageId}') returned false — page may not be registered");
            }
        }
    }

    private static bool BrowseHistoryEmpty(GuiDialogHandbook dialog)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var bhField = typeof(GuiDialogHandbook).GetField("browseHistory", flags);
        if (bhField?.GetValue(dialog) is System.Collections.ICollection coll) return coll.Count == 0;
        return true;
    }

    private void DumpInternalState(GuiDialogHandbook dialog, string when)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var t = typeof(GuiDialogHandbook);

        int browseCount = -1;
        var bhField = t.GetField("browseHistory", flags);
        if (bhField?.GetValue(dialog) is System.Collections.ICollection coll) browseCount = coll.Count;

        string singleComposerName = "<unknown>";
        var scProp = dialog.GetType().GetProperty("SingleComposer", flags);
        if (scProp?.GetValue(dialog) is object sc)
        {
            var nameField = sc.GetType().GetField("dialogName", flags) ?? sc.GetType().GetField("name", flags);
            singleComposerName = nameField?.GetValue(sc)?.ToString() ?? sc.GetType().Name;
        }
        else
        {
            singleComposerName = "<null>";
        }

        var detailField = t.GetField("detailViewGui", flags);
        var overviewField = t.GetField("overviewGui", flags);
        bool detailNull = detailField?.GetValue(dialog) == null;
        bool overviewNull = overviewField?.GetValue(dialog) == null;

        CodexLogger.Info(capi, "handbook-tab",
            $"state {when}: browseHistory.Count={browseCount} singleComposer='{singleComposerName}' detailNull={detailNull} overviewNull={overviewNull} opened={dialog.IsOpened()}");
    }
}
