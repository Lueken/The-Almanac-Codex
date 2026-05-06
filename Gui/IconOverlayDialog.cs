using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AlmanacCodex.Gui;

/// <summary>
/// Vanilla VS GuiDialog that paints over the ImGui Almanac dialog. Two responsibilities:
/// 1. Draw item icons via the full VS shape/model pipeline (RenderItemstackToGui), since
///    ImGui has no native bridge for that.
/// 2. Draw a custom tooltip on top of icons (replacing ImGui's built-in tooltip, which
///    rendered behind our overlay due to draw-order).
/// </summary>
public class IconOverlayDialog : GuiDialog
{
    public readonly List<IconRenderRequest> Requests = new();

    /// <summary>Cell labels (entry numbers) drawn AFTER icons so they sit on top.</summary>
    public readonly List<CellLabelRequest> CellLabels = new();

    /// <summary>Set per-frame by AlmanacDialog. If non-null, scissor-clips icons to this rect.</summary>
    public ClipRect? ClipBounds;

    /// <summary>Set per-frame by AlmanacDialog when a cell is hovered.</summary>
    public TooltipState? Tooltip;

    /// <summary>When true, suppress all icons + cell labels (e.g. while an ImGui popup is open
    /// so the popup remains the most-forward element).</summary>
    public bool SuppressOverlay;

    private readonly Dictionary<string, LoadedTexture> tooltipLineCache = new();
    private readonly Dictionary<string, LoadedTexture> cellLabelCache = new();

    public IconOverlayDialog(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => "almanaccodex-icon-overlay";

    // Render AFTER ImGui (vsimgui host dialog uses default 0.1). Icons paint over the
    // ImGui window's opaque background. Tooltip is drawn last so it sits over icons.
    public override double DrawOrder => 1.5;

    public override bool Focusable => false;
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    public override EnumDialogType DialogType => EnumDialogType.HUD;

    public override void OnGuiOpened() { }

    public override void OnRenderGUI(float deltaTime)
    {
        if (SuppressOverlay)
        {
            Requests.Clear();
            CellLabels.Clear();
            ClipBounds = null;
            Tooltip = null;
            SuppressOverlay = false;
            return;
        }

        bool scissored = false;
        if (ClipBounds is { } cb)
        {
            // PushScissor with ElementBounds handles GUIScale + Y-axis conversion. Raw GlScissor
            // wasn't catching RenderItemstackToGui — items overflowed past the dialog edge.
            var bounds = Vintagestory.API.Client.ElementBounds
                .Fixed(cb.X / RuntimeEnv.GUIScale, cb.Y / RuntimeEnv.GUIScale,
                       cb.Width / RuntimeEnv.GUIScale, cb.Height / RuntimeEnv.GUIScale);
            bounds.ParentBounds = capi.Gui.WindowBounds;
            bounds.CalcWorldBounds();
            if (bounds.InnerWidth > 0 && bounds.InnerHeight > 0)
            {
                capi.Render.PushScissor(bounds);
                scissored = true;
            }
        }

        foreach (var req in Requests)
        {
            capi.Render.RenderItemstackToGui(
                new DummySlot(req.Stack),
                req.X + req.Size / 2.0,
                req.Y,
                100,
                (float)req.Size,
                req.ColorArgb,
                shading: true,
                rotate: false,
                showStackSize: false);
        }

        if (scissored) capi.Render.PopScissor();

        // Cell labels (entry numbers) — rendered after icons with depth-test disabled so the
        // 3-digit number always sits on top of the specimen art.
        if (CellLabels.Count > 0)
        {
            capi.Render.GLDisableDepthTest();
            foreach (var lbl in CellLabels)
            {
                DrawCellLabel(lbl);
            }
            capi.Render.GLEnableDepthTest();
        }

        // Tooltip is drawn unclipped, after icons, with depth test disabled so it always
        // wins against the item icons (whose render path uses depth and resists 2D layering).
        if (Tooltip is { } tt)
        {
            capi.Render.GLDisableDepthTest();
            DrawTooltip(tt);
            capi.Render.GLEnableDepthTest();
        }

        Requests.Clear();
        CellLabels.Clear();
        ClipBounds = null;
        Tooltip = null;
    }

    private void DrawCellLabel(CellLabelRequest lbl)
    {
        // Cache key = text + style (gold for hover/selected, muted otherwise). Cap is ~232 entries
        // worst case; in practice only what fits on-screen at once.
        var key = lbl.Text + (lbl.Highlighted ? "_g" : "_m");
        if (!cellLabelCache.TryGetValue(key, out var tex))
        {
            // Color triplet (text, border, fill) chosen so the chip reads against a busy icon
            // behind it. Highlighted = gold border + gold text. Muted = hairline border + ivory.
            var textColor = lbl.Highlighted
                ? new double[] { 0xd4 / 255.0, 0xa8 / 255.0, 0x5a / 255.0, 1.0 }   // GoldAccent
                : new double[] { 0xe8 / 255.0, 0xd9 / 255.0, 0xb8 / 255.0, 1.0 };  // InkPrimary
            var borderColor = lbl.Highlighted
                ? new[] { 0xd4 / 255.0, 0xa8 / 255.0, 0x5a / 255.0, 1.0 }
                : new[] { 0x5a / 255.0, 0x4a / 255.0, 0x36 / 255.0, 1.0 };

            var font = CairoFont.WhiteSmallText().WithFontSize(11).WithColor(textColor);
            font.Fontname = "Georgia";
            var bg = new TextBackground
            {
                HorPadding = 5,
                VerPadding = 2,
                Radius = 2,
                FillColor = new[] { 0x1f / 255.0, 0x18 / 255.0, 0x10 / 255.0, 0.92 },  // InsetBg @ 92%
                BorderColor = borderColor,
                BorderWidth = 1,
            };
            tex = new TextTextureUtil(capi).GenTextTexture(lbl.Text, font, 80, bg);
            cellLabelCache[key] = tex;
        }
        // Right-anchor: lbl.X is the cell's right edge minus a margin; compute draw X from texture width.
        double drawX = lbl.RightAnchor ? lbl.X - tex.Width : lbl.X;
        capi.Render.Render2DTexturePremultipliedAlpha(tex.TextureId, drawX, lbl.Y, tex.Width, tex.Height, z: 400);
    }

    private void DrawTooltip(TooltipState tt)
    {
        // Bake the multi-line tooltip (bg + border + text) as a single Cairo-rendered texture
        // and render it via Render2DTexturePremultipliedAlpha. This avoids RenderRectangle —
        // which silently fails to paint at this stage in our HUD-overlay pipeline — and keeps
        // bg + text in one premultiplied-alpha texture that composes cleanly on top of icons.
        if (tt.Lines.Length == 0) return;

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < tt.Lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(tt.Lines[i].Text);
        }
        var key = sb.ToString();

        if (!tooltipLineCache.TryGetValue(key, out var tex))
        {
            // Codex parchment palette — matches CodexTheme:
            //   ParchmentBg     #332a1f  → fill
            //   BorderHairline  #5a4a36  → border
            //   InkPrimary      #e8d9b8  → text
            // Cairo font family "Georgia" resolves to the system font when present, falling
            // back gracefully to a generic serif if not.
            var font = CairoFont.WhiteSmallText()
                .WithFontSize(15)
                .WithColor(new double[] { 0xe8 / 255.0, 0xd9 / 255.0, 0xb8 / 255.0, 1.0 });
            // Cairo resolves a serif from the system font config. Setting the family directly
            // (CairoFont has no WithFamily fluent API in this VS version).
            font.Fontname = "Georgia";
            var bg = new TextBackground
            {
                HorPadding = 10,
                VerPadding = 7,
                Radius = 2,
                FillColor = new[] { 0x33 / 255.0, 0x2a / 255.0, 0x1f / 255.0, 0.96 },
                BorderColor = new[] { 0x5a / 255.0, 0x4a / 255.0, 0x36 / 255.0, 1.0 },
                BorderWidth = 1,
            };
            tex = new TextTextureUtil(capi).GenTextTexture(key, font, 320, bg);
            tooltipLineCache[key] = tex;
        }

        double boxX = tt.X + 14;
        double boxY = tt.Y + 14;

        int fbW = capi.Render.FrameWidth;
        int fbH = capi.Render.FrameHeight;
        if (boxX + tex.Width > fbW) boxX = fbW - tex.Width - 4;
        if (boxY + tex.Height > fbH) boxY = fbH - tex.Height - 4;
        if (boxX < 0) boxX = 0;
        if (boxY < 0) boxY = 0;

        capi.Render.Render2DTexturePremultipliedAlpha(
            tex.TextureId, boxX, boxY, tex.Width, tex.Height, z: 500);
    }

    private static CairoFont FadedFont()
    {
        return CairoFont.WhiteSmallText().WithColor(new double[] { 0.65, 0.65, 0.65, 1.0 });
    }

    public override void Dispose()
    {
        foreach (var tex in tooltipLineCache.Values) tex.Dispose();
        tooltipLineCache.Clear();
        foreach (var tex in cellLabelCache.Values) tex.Dispose();
        cellLabelCache.Clear();
        base.Dispose();
    }
}

public readonly struct CellLabelRequest
{
    public readonly string Text;
    public readonly float X;
    public readonly float Y;
    public readonly bool Highlighted;
    public readonly bool RightAnchor;  // when true, X is the right edge of the chip
    public CellLabelRequest(string text, float x, float y, bool highlighted, bool rightAnchor = false)
    {
        Text = text; X = x; Y = y; Highlighted = highlighted; RightAnchor = rightAnchor;
    }
}

public readonly struct ClipRect
{
    public readonly float X, Y, Width, Height;
    public ClipRect(float x, float y, float w, float h) { X = x; Y = y; Width = w; Height = h; }
}

public readonly struct TooltipState
{
    public readonly TooltipLine[] Lines;
    public readonly float X;
    public readonly float Y;
    public TooltipState(TooltipLine[] lines, float x, float y) { Lines = lines; X = x; Y = y; }
}

public readonly struct TooltipLine
{
    public readonly string Text;
    public readonly bool Faded;
    public TooltipLine(string text, bool faded) { Text = text; Faded = faded; }
}

public readonly struct IconRenderRequest
{
    public readonly ItemStack Stack;
    public readonly float X;
    public readonly float Y;
    public readonly float Size;
    public readonly int ColorArgb;

    public IconRenderRequest(ItemStack stack, float x, float y, float size, int colorArgb)
    {
        Stack = stack; X = x; Y = y; Size = size; ColorArgb = colorArgb;
    }
}
