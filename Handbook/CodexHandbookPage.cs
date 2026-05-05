using System.Collections.Generic;
using System.Text;
using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace AlmanacCodex.Handbook;

public class CodexHandbookPage : GuiHandbookPage
{
    public const string Category = "almanac";
    public const string PageId = "almanac:launchpad";

    private readonly ICoreClientAPI capi;
    private readonly AlmanacEntryRegistry entries;
    private readonly DiscoveryStore store;
    private readonly IReadOnlyList<SubmodCard> submodCards;

    private LoadedTexture? titleTexture;
    private string titleCached = "";

    public CodexHandbookPage(ICoreClientAPI capi, AlmanacEntryRegistry entries, DiscoveryStore store, IReadOnlyList<SubmodCard> submodCards)
    {
        this.capi = capi;
        this.entries = entries;
        this.store = store;
        this.submodCards = submodCards;
        Visible = true;
    }

    public override string PageCode => PageId;
    public override string CategoryCode => Category;
    public override bool IsDuplicate => false;
    public override float SearchWeightOffset => 0f;

    public override PageText GetPageText()
    {
        EnsureTitleCached();
        return new PageText { Title = titleCached, Text = "" };
    }

    public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
    {
        EnsureTitleCached();
        if (titleTexture == null)
        {
            titleTexture = new TextTextureUtil(capi).GenTextTexture(titleCached, CairoFont.WhiteSmallText());
        }

        double pad = GuiElement.scaled(10);
        capi.Render.Render2DTexturePremultipliedAlpha(
            titleTexture.TextureId,
            x + pad,
            y + (cellHeight - titleTexture.Height) / 2,
            titleTexture.Width,
            titleTexture.Height);
    }

    public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<font size=\"24\"><strong>{Lang.Get("almanaccodex:launchpad-welcome-heading")}</strong></font>");
        sb.AppendLine();
        sb.AppendLine(Lang.Get("almanaccodex:launchpad-welcome-body"));
        sb.AppendLine();
        sb.AppendLine($"<i>{Lang.Get("almanaccodex:launchpad-hotkey-hint")}</i>");
        sb.AppendLine();
        sb.AppendLine($"<font size=\"18\"><strong>{Lang.Get("almanaccodex:launchpad-submods-heading")}</strong></font>");
        sb.AppendLine();

        foreach (var card in submodCards)
        {
            sb.AppendLine($"<strong>{Lang.Get(card.NameKey)}</strong>");
            switch (card.Status)
            {
                case SubmodStatus.Loaded:
                    int discovered = CountDiscoveredFor(card.OwnerModId);
                    int total = CountRegisteredFor(card.OwnerModId);
                    sb.AppendLine($"<i>{Lang.Get("almanaccodex:launchpad-submod-loaded")} — {Lang.Get("almanaccodex:launchpad-submod-progress", discovered, total)}</i>");
                    break;
                case SubmodStatus.InDevelopment:
                    sb.AppendLine($"<i>{Lang.Get("almanaccodex:launchpad-submod-in-development")}</i>");
                    break;
                default:
                    sb.AppendLine($"<i>{Lang.Get("almanaccodex:launchpad-submod-not-loaded")}</i>");
                    break;
            }
            sb.AppendLine(Lang.Get(card.BlurbKey));
            sb.AppendLine($"<i>→ {Lang.Get(card.GetStartedKey)}</i>");
            sb.AppendLine();
        }

        var richtext = VtmlUtil.Richtextify(capi, sb.ToString(), CairoFont.WhiteSmallText().WithLineHeightMultiplier(1.2));
        detailViewGui.AddRichtext(richtext, textBounds, "richtext");
    }

    public override void Dispose()
    {
        titleTexture?.Dispose();
        titleTexture = null;
    }

    private void EnsureTitleCached()
    {
        if (titleCached.Length > 0) return;
        titleCached = Lang.Get("almanaccodex:launchpad-welcome-heading");
    }

    private int CountRegisteredFor(string ownerModId)
    {
        int n = 0;
        foreach (var e in entries.All)
        {
            if (e.OwnerModId == ownerModId) n++;
        }
        return n;
    }

    private int CountDiscoveredFor(string ownerModId)
    {
        var player = capi.World.Player;
        int n = 0;
        foreach (var e in entries.All)
        {
            if (e.OwnerModId != ownerModId) continue;
            if (store.GetStage(player, e.Code.ToShortString()) != DiscoveryStage.Unknown) n++;
        }
        return n;
    }
}
