using AlmanacCodex.Registry;
using AlmanacCodex.State;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AlmanacCodex.Handbook;

public class CodexHandbookPage : GuiHandbookPage
{
    public const string Category = "almanac";

    private readonly ICoreClientAPI capi;
    private readonly AlmanacEntry entry;
    private readonly DiscoveryStore store;
    private readonly ItemStack? displayStack;

    private LoadedTexture? titleTexture;
    private string titleCached = "";

    public CodexHandbookPage(ICoreClientAPI capi, AlmanacEntry entry, DiscoveryStore store)
    {
        this.capi = capi;
        this.entry = entry;
        this.store = store;

        var collectible = capi.World.GetBlock(entry.Code) as CollectibleObject
            ?? capi.World.GetItem(entry.Code) as CollectibleObject;
        if (collectible != null)
        {
            displayStack = new ItemStack(collectible);
        }

        Visible = false;
    }

    public override string PageCode => "almanac:" + entry.Code.ToShortString();
    public override string CategoryCode => Category;
    public override bool IsDuplicate => false;
    public override float SearchWeightOffset => 0f;

    public AssetLocation EntryCode => entry.Code;

    public DiscoveryStage CurrentStage => store.GetStage(capi.World.Player, entry.Code.ToShortString());

    public void RefreshVisibility()
    {
        Visible = CurrentStage != DiscoveryStage.Unknown;
    }

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
        double iconSize = GuiElement.scaled(25);

        if (displayStack != null)
        {
            capi.Render.RenderItemstackToGui(
                new DummySlot(displayStack),
                x + pad + iconSize / 2,
                y + cellHeight / 2,
                100,
                (float)iconSize,
                ColorUtil.WhiteArgb,
                shading: true,
                rotate: false,
                showStackSize: false);
        }

        capi.Render.Render2DTexturePremultipliedAlpha(
            titleTexture.TextureId,
            x + pad + iconSize + GuiElement.scaled(10),
            y + (cellHeight - titleTexture.Height) / 2,
            titleTexture.Width,
            titleTexture.Height);
    }

    public override void ComposePage(GuiComposer detailViewGui, ElementBounds textBounds, ItemStack[] allstacks, ActionConsumable<string> openDetailPageFor)
    {
        var stage = CurrentStage;
        var name = displayStack?.GetName() ?? entry.Code.ToShortString();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<font size=\"24\"><strong>{name}</strong></font>");
        sb.AppendLine();
        sb.AppendLine($"<i>Discovery: {stage}</i>");
        sb.AppendLine();

        if (stage >= DiscoveryStage.Held && displayStack != null)
        {
            sb.AppendLine("<strong>Description</strong>");
            sb.AppendLine(displayStack.Collectible.GetHeldItemName(displayStack) + ".");
            sb.AppendLine();
            sb.AppendLine("<strong>Tags</strong>");
            var tagNames = capi.CollectibleTagRegistry.SlowEnumerateTagNames(displayStack.Collectible.Tags);
            sb.AppendLine(string.Join(", ", tagNames));
        }
        else
        {
            sb.AppendLine("<i>Pick this up to learn its traits.</i>");
        }

        sb.AppendLine();
        sb.AppendLine("<strong>Applications</strong>");
        if (stage >= DiscoveryStage.Held)
        {
            sb.AppendLine("<i>(processes will appear here as you try them — coming in v0.2.0)</i>");
        }
        else
        {
            sb.AppendLine("<i>???</i>");
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
        titleCached = displayStack?.GetName() ?? entry.Code.ToShortString();
    }
}
