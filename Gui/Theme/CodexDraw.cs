using System;
using System.Numerics;
using AlmanacCodex.State;
using ImGuiNET;

namespace AlmanacCodex.Gui.Theme;

/// <summary>
/// Custom draw-list helpers for elements ImGui doesn't render natively the way the
/// brand SVGs require: letter-spaced overlines, dashed-border rectangles, wax-seal
/// circles, stage dots, hairline dividers, pill chips. Each helper takes an explicit
/// <see cref="ImDrawListPtr"/> and absolute screen-space coords so it works inside
/// any ImGui window, regardless of cursor / child state.
/// </summary>
public static class CodexDraw
{
    // ── Hairlines ────────────────────────────────────────────────────────────
    public static void Hairline(ImDrawListPtr dl, float x1, float y1, float x2, float y2)
        => Hairline(dl, x1, y1, x2, y2, CodexTheme.U(CodexTheme.BorderHairline));

    public static void Hairline(ImDrawListPtr dl, float x1, float y1, float x2, float y2, uint color)
        => dl.AddLine(new Vector2(x1, y1), new Vector2(x2, y2), color, CodexTheme.StrokeHairline);

    // ── Letter-spaced text (used by overline labels) ─────────────────────────
    /// <summary>
    /// Draws <paramref name="text"/> at <paramref name="pos"/> with each character's advance
    /// stretched by <paramref name="letterSpacing"/> px. Caller must <see cref="ImGui.PushFont"/>
    /// the desired size first; widths are measured against the active font.
    /// </summary>
    public static float TextLetterSpaced(ImDrawListPtr dl, Vector2 pos, uint color, string text, float letterSpacing)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float x = pos.X;
        for (int i = 0; i < text.Length; i++)
        {
            var s = text[i].ToString();
            dl.AddText(new Vector2(x, pos.Y), color, s);
            x += ImGui.CalcTextSize(s).X + letterSpacing;
        }
        return x - pos.X;
    }

    /// <summary>
    /// Draws an overline label: uppercase, letter-spaced, in the muted-label color. Default size
    /// is the 10pt scale; pass <paramref name="fontSize"/> to override (use 12pt in detail-panel
    /// section labels). Caller controls position; cursor is not advanced.
    /// </summary>
    public static void DrawOverline(string text, Vector2 pos, uint? colorOverride = null, int? fontSize = null)
    {
        var size = fontSize ?? (int)CodexTheme.FontOverline;
        ImGui.PushFont(CodexFonts.Get(size));
        var dl = ImGui.GetWindowDrawList();
        var color = colorOverride ?? CodexTheme.U(CodexTheme.InkLabel);
        TextLetterSpaced(dl, pos, color, text.ToUpperInvariant(), CodexTheme.LetterSpacingOverline);
        ImGui.PopFont();
    }

    // ── Dashed-border rectangle (for untried processes / undiscovered variants) ────
    public static void DashedRect(ImDrawListPtr dl, Vector2 min, Vector2 max, uint color,
        float dashLength = 3f, float dashGap = 3f, float rounding = 0f)
    {
        // ImGui has no native dashed stroke. Emit short line segments along each edge.
        // Rounded corners are skipped to keep the implementation compact; for small radii
        // (3-4px) the corner squareness is acceptable per the SVG.
        DashedSegment(dl, new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), color, dashLength, dashGap); // top
        DashedSegment(dl, new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), color, dashLength, dashGap); // right
        DashedSegment(dl, new Vector2(max.X, max.Y), new Vector2(min.X, max.Y), color, dashLength, dashGap); // bottom
        DashedSegment(dl, new Vector2(min.X, max.Y), new Vector2(min.X, min.Y), color, dashLength, dashGap); // left
    }

    private static void DashedSegment(ImDrawListPtr dl, Vector2 a, Vector2 b, uint color, float dashLength, float dashGap)
    {
        var diff = new Vector2(b.X - a.X, b.Y - a.Y);
        float len = MathF.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
        if (len <= 0.001f) return;
        var dir = new Vector2(diff.X / len, diff.Y / len);
        float t = 0f;
        while (t < len)
        {
            float t2 = MathF.Min(t + dashLength, len);
            dl.AddLine(
                new Vector2(a.X + dir.X * t,  a.Y + dir.Y * t),
                new Vector2(a.X + dir.X * t2, a.Y + dir.Y * t2),
                color, CodexTheme.StrokeHairline);
            t = t2 + dashGap;
        }
    }

    // ── Stage dots (3-dot indicator at bottom of grid cells / in legend) ─────
    public static void StageDots(ImDrawListPtr dl, Vector2 center, DiscoveryStage stage)
    {
        // 3 dots, 2px radius, 6px stride. Filled gold for "achieved" levels;
        // hollow with hairline border for un-achieved.
        var fill = CodexTheme.U(CodexTheme.GoldAccent);
        var emptyFill = CodexTheme.U(CodexTheme.ChipBgWarm);
        var emptyStroke = CodexTheme.U(CodexTheme.BorderHairline);

        for (int i = 0; i < 3; i++)
        {
            var c = new Vector2(center.X + i * CodexTheme.StageDotStride, center.Y);
            int level = i + 1;
            bool achieved = (int)stage >= level;
            if (achieved)
            {
                dl.AddCircleFilled(c, CodexTheme.StageDotRadius, fill);
            }
            else
            {
                dl.AddCircleFilled(c, CodexTheme.StageDotRadius, emptyFill);
                dl.AddCircle(c, CodexTheme.StageDotRadius, emptyStroke, 0, CodexTheme.StageDotEmptyStroke);
            }
        }
    }

    // ── Wax seal (Sighted / Held / Processed) ────────────────────────────────
    /// <summary>
    /// Draws a circular seal at <paramref name="center"/>: outer-ring + inner-ring + uppercase
    /// label + glyph (✓ for active stages, "N/M" for the Processed seal). Outer radius
    /// <see cref="CodexTheme.SealRadius"/>; total bbox <see cref="CodexTheme.SealBoxSize"/>.
    /// </summary>
    public static void DrawSeal(ImDrawListPtr dl, Vector2 center, string label, bool active, string? ratio = null)
    {
        var palette = CodexTheme.GetSealColors(active);
        var fill = CodexTheme.U(palette.Fill);
        var ring = CodexTheme.U(palette.Ring);
        var labelColor = CodexTheme.U(palette.Label);
        var glyphColor = CodexTheme.U(palette.Glyph);

        // Outer ring
        dl.AddCircleFilled(center, CodexTheme.SealRadius, fill);
        dl.AddCircle(center, CodexTheme.SealRadius, ring, 0, palette.RingStroke);
        // Inner ring (subtle hairline; only on active seals per the SVG)
        if (active)
        {
            dl.AddCircle(center, CodexTheme.SealInnerRadius, ring, 0, CodexTheme.StrokeInnerRing);
        }

        // Label (uppercase, 10pt with generous letter-spacing for breathing room)
        ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontOverline));
        var labelWidth = MeasureLetterSpaced(label, CodexTheme.LetterSpacingSeal);
        var labelPos = new Vector2(center.X - labelWidth / 2f, center.Y - CodexTheme.SealRadius * 0.40f);
        TextLetterSpaced(dl, labelPos, labelColor, label, CodexTheme.LetterSpacingSeal);
        ImGui.PopFont();

        // Glyph below label
        if (ratio != null)
        {
            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontStageRatio));
            var rsize = ImGui.CalcTextSize(ratio);
            dl.AddText(new Vector2(center.X - rsize.X / 2f, center.Y + 2f), glyphColor, ratio);
            ImGui.PopFont();
        }
        else if (active)
        {
            // Custom-drawn checkmark — Georgia & DejaVu both have glyph U+2713 but font may
            // not include it; safer to render as two line segments for a guaranteed shape.
            float cx = center.X;
            float cy = center.Y + CodexTheme.SealRadius * 0.30f;
            dl.AddLine(new Vector2(cx - 6, cy - 1), new Vector2(cx - 1, cy + 4), glyphColor, 2f);
            dl.AddLine(new Vector2(cx - 1, cy + 4), new Vector2(cx + 7, cy - 5), glyphColor, 2f);
        }
    }

    public static float FontStageRatio => CodexTheme.FontStageRatio;

    // ── Pill chip (used for properties + filter pills) ───────────────────────
    public static void DrawPillChip(ImDrawListPtr dl, Vector2 pos, float width, string label, CodexTheme.ChipColors colors, int fontSize = 13)
    {
        float h = CodexTheme.ChipHeight;
        var min = pos;
        var max = new Vector2(pos.X + width, pos.Y + h);
        dl.AddRectFilled(min, max, CodexTheme.U(colors.Bg), CodexTheme.RadiusPill);
        dl.AddRect(min, max, CodexTheme.U(colors.Border), CodexTheme.RadiusPill, ImDrawFlags.None, CodexTheme.StrokeHairline);

        ImGui.PushFont(CodexFonts.Get(fontSize));
        var ts = ImGui.CalcTextSize(label);
        dl.AddText(new Vector2(pos.X + (width - ts.X) / 2f, pos.Y + (h - ts.Y) / 2f),
            CodexTheme.U(colors.Text), label);
        ImGui.PopFont();
    }

    // ── Variant tab (small rect in detail panel "VARIANTS" row) ─────────────
    public static void DrawVariantTab(ImDrawListPtr dl, Vector2 pos, string label, bool known)
    {
        float w = CodexTheme.VariantTabW;
        float h = CodexTheme.VariantTabH;
        var min = pos;
        var max = new Vector2(pos.X + w, pos.Y + h);

        if (known)
        {
            dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.ChipBgWarm), CodexTheme.RadiusVariantTab);
            dl.AddRect(min, max, CodexTheme.U(CodexTheme.GoldAccent), CodexTheme.RadiusVariantTab, ImDrawFlags.None, CodexTheme.StrokeHairline);

            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
            var ts = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(pos.X + (w - ts.X) / 2f, pos.Y + (h - ts.Y) / 2f),
                CodexTheme.U(CodexTheme.InkPrimary), label);
            ImGui.PopFont();
        }
        else
        {
            dl.AddRectFilled(min, max, CodexTheme.U(CodexTheme.InsetBg), CodexTheme.RadiusVariantTab);
            DashedRect(dl, min, max, CodexTheme.U(CodexTheme.BorderHairline),
                CodexTheme.DashLengthSmall, CodexTheme.DashGapSmall, CodexTheme.RadiusVariantTab);

            ImGui.PushFont(CodexFonts.Get((int)CodexTheme.FontTitle));
            var ts = ImGui.CalcTextSize(label);
            dl.AddText(new Vector2(pos.X + (w - ts.X) / 2f, pos.Y + (h - ts.Y) / 2f),
                CodexTheme.U(CodexTheme.InkDisabled), label);
            ImGui.PopFont();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    public static float MeasureLetterSpaced(string text, float letterSpacing)
    {
        if (string.IsNullOrEmpty(text)) return 0f;
        float w = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            w += ImGui.CalcTextSize(text[i].ToString()).X;
            if (i < text.Length - 1) w += letterSpacing;
        }
        return w;
    }
}
