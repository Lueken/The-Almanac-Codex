using System.Numerics;
using ImGuiNET;

namespace AlmanacCodex.Gui.Theme;

/// <summary>
/// Pushes/pops the Codex's ImGui style stack so the dialog chrome reads as parchment instead
/// of vsimgui's debug defaults. Call <see cref="Push"/> on entry to <c>OnDraw</c>, <see cref="Pop"/>
/// on every return path.
///
/// Counts are kept in sync via <see cref="ColorCount"/> / <see cref="VarCount"/> so the Pop
/// reverses exactly what was pushed.
/// </summary>
public static class CodexStyle
{
    private const int ColorCount = 22;
    private const int VarCount = 7;

    public static void Push()
    {
        // ── Surfaces ────────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.WindowBg,        CodexTheme.ParchmentBg);
        ImGui.PushStyleColor(ImGuiCol.ChildBg,         CodexTheme.ParchmentBg);
        ImGui.PushStyleColor(ImGuiCol.PopupBg,         CodexTheme.ParchmentRaised);
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg,       CodexTheme.ParchmentRaised);

        // ── Borders ─────────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Border,          CodexTheme.BorderHairline);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow,    new Vector4(0, 0, 0, 0));

        // ── Text ────────────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Text,            CodexTheme.InkPrimary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled,    CodexTheme.InkDisabled);

        // ── Frames (input fields, search box) ───────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.FrameBg,         CodexTheme.InsetBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,  Lighten(CodexTheme.InsetBg, 0.05f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive,   Lighten(CodexTheme.InsetBg, 0.10f));

        // ── Buttons (used for category rows + the like) ─────────────────────
        ImGui.PushStyleColor(ImGuiCol.Button,          CodexTheme.ChipBgWarm);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,   Lighten(CodexTheme.ChipBgWarm, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,    Lighten(CodexTheme.ChipBgWarm, 0.16f));

        // ── Headers (Selectables, CollapsingHeaders) ────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Header,          CodexTheme.ChipBgWarm);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,   Lighten(CodexTheme.ChipBgWarm, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive,    Lighten(CodexTheme.ChipBgWarm, 0.16f));

        // ── Scrollbar ───────────────────────────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg,         CodexTheme.WithAlpha(CodexTheme.InsetBg, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab,       CodexTheme.WithAlpha(CodexTheme.BorderHairline, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, CodexTheme.GoldAccent);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive,  CodexTheme.GoldAccent);

        // ── Separator (horizontal dividers) ─────────────────────────────────
        ImGui.PushStyleColor(ImGuiCol.Separator,       CodexTheme.BorderHairline);

        // ── Style vars (rounding + spacing) ─────────────────────────────────
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding,  CodexTheme.RadiusWindow);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,   CodexTheme.RadiusFrame);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,   CodexTheme.RadiusFrame);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding,   CodexTheme.RadiusFrame);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, CodexTheme.StrokeStandard);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize,  CodexTheme.StrokeHairline);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,
            new Vector2(CodexTheme.SpaceSm, CodexTheme.SpaceSm));
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(VarCount);
        ImGui.PopStyleColor(ColorCount);
    }

    private static Vector4 Lighten(Vector4 c, float amount) => new(
        Min1(c.X + amount),
        Min1(c.Y + amount),
        Min1(c.Z + amount),
        c.W);

    private static float Min1(float v) => v > 1f ? 1f : v;
}
