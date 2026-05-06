using System.Numerics;
using ImGuiNET;

namespace AlmanacCodex.Gui.Theme;

/// <summary>
/// Single source of truth for Codex visual tokens. Derived from the brand SVGs:
///   - almanac_codex_index_view_v2_silhouettes.svg
///   - almanac_codex_detail_concept_3_stage_seals.svg
///
/// Color names describe role (Parchment, Ink, Border, Seal, Chip, ...) not raw value, so
/// renderers can be retoned later by editing one place. All colors are stored as
/// <see cref="Vector4"/> (ImGui's float4 form); use <see cref="U(Vector4)"/> for the packed
/// uint required by <c>ImDrawListPtr</c> calls.
/// </summary>
public static class CodexTheme
{
    // ── Surface palette ──────────────────────────────────────────────────────
    public static readonly Vector4 ParchmentBg     = Hex(0x332a1f); // window body
    public static readonly Vector4 ParchmentRaised = Hex(0x3d3224); // top bar / raised band
    public static readonly Vector4 InsetBg         = Hex(0x1f1810); // grid cell, search box, processed seal interior
    public static readonly Vector4 InsetDeep       = Hex(0x0f0a06); // soft drop shadows
    public static readonly Vector4 SealBgActive    = Hex(0x3d2818); // active wax-seal fill
    public static readonly Vector4 ChipBgWarm      = Hex(0x3a2f22); // category pill, variant tab

    // ── Borders ──────────────────────────────────────────────────────────────
    public static readonly Vector4 BorderHairline = Hex(0x5a4a36); // default 0.5 stroke
    public static readonly Vector4 BorderActive   = Hex(0xd4a85a); // gold border (selected cell, active tab)

    // ── Ink (text) ───────────────────────────────────────────────────────────
    public static readonly Vector4 InkPrimary   = Hex(0xe8d9b8); // ivory body / titles
    public static readonly Vector4 InkSecondary = Hex(0xa89776); // muted body / Latin name
    public static readonly Vector4 InkLabel     = Hex(0x8a7958); // overline labels (HABITAT, CATEGORIES)
    public static readonly Vector4 InkMuted     = Hex(0x6a5a42); // dim text (counts, captions)
    public static readonly Vector4 InkDisabled  = Hex(0x5a4a36); // unsighted / untried text

    public static readonly Vector4 GoldAccent = Hex(0xd4a85a); // discovery counter, active tags

    // ── Tag-chip color triples (bg, border, text) ────────────────────────────
    public readonly record struct ChipColors(Vector4 Bg, Vector4 Border, Vector4 Text);

    public static readonly ChipColors ChipDefault     = new(Hex(0x3a2f22), Hex(0x5a4a36), Hex(0xa89776));
    public static readonly ChipColors ChipToxic       = new(Hex(0x3d2818), Hex(0x8a4a2a), Hex(0xd47050)); // warm red-amber
    public static readonly ChipColors ChipMedicinal   = new(Hex(0x1a3024), Hex(0x3a6b4a), Hex(0x7ac094)); // green
    public static readonly ChipColors ChipFibrous     = new(Hex(0x3d2818), Hex(0x8a4a2a), Hex(0xd4a070)); // tan
    public static readonly ChipColors ChipPsychoactive = new(Hex(0x2a1a3a), Hex(0x5a3a8a), Hex(0xa890d4)); // violet
    public static readonly ChipColors ChipCulinary    = new(Hex(0x2a2418), Hex(0x6b5a2a), Hex(0xd4c070)); // wheat
    public static readonly ChipColors ChipAromatic    = new(Hex(0x2a2418), Hex(0x6b5a2a), Hex(0xd4c070));
    public static readonly ChipColors ChipDecorative  = new(Hex(0x2a2a2a), Hex(0x5a5a5a), Hex(0xb0b0b0));
    public static readonly ChipColors ChipSweet       = new(Hex(0x3a2418), Hex(0x8a5a2a), Hex(0xd4a070));
    public static readonly ChipColors ChipAcidic      = new(Hex(0x2a3018), Hex(0x6b8a2a), Hex(0xc0d470));
    public static readonly ChipColors ChipStarchy     = new(Hex(0x2a2418), Hex(0x6b5a2a), Hex(0xc0a878));
    public static readonly ChipColors ChipLeafy       = new(Hex(0x1a3024), Hex(0x3a6b4a), Hex(0x90c094));
    public static readonly ChipColors ChipSeedy       = new(Hex(0x2a2418), Hex(0x6b5a2a), Hex(0xc0a878));
    public static readonly ChipColors ChipFruity      = new(Hex(0x3a1f24), Hex(0x8a3a4a), Hex(0xd47090));

    public static ChipColors GetChipColors(string tagSlug) => tagSlug.ToLowerInvariant() switch
    {
        "toxic"        => ChipToxic,
        "medicinal"    => ChipMedicinal,
        "fibrous"      => ChipFibrous,
        "psychoactive" => ChipPsychoactive,
        "culinary"     => ChipCulinary,
        "aromatic"     => ChipAromatic,
        "decorative"   => ChipDecorative,
        "sweet"        => ChipSweet,
        "acidic"       => ChipAcidic,
        "starchy"      => ChipStarchy,
        "leafy"        => ChipLeafy,
        "seedy"        => ChipSeedy,
        "fruity"       => ChipFruity,
        _              => ChipDefault,
    };

    // ── Process card (DONE / UNTRIED) ─────────────────────────────────────────
    public static readonly Vector4 ProcessDoneBg       = Hex(0x1a2a1a);
    public static readonly Vector4 ProcessDoneHeader   = Hex(0x1f3024);
    public static readonly Vector4 ProcessDoneBorder   = Hex(0x3a6b3a);
    public static readonly Vector4 ProcessDoneTitle    = Hex(0x7ac094);
    public static readonly Vector4 ProcessDoneOutcome  = Hex(0xc8e0c8);
    public static readonly Vector4 ProcessDoneFlavor   = Hex(0x8aa88a);
    public static readonly Vector4 ProcessDoneHint     = Hex(0x5a7a5a);

    public static readonly Vector4 ProcessUntriedBorder = BorderHairline; // dashed
    public static readonly Vector4 ProcessUntriedTitle  = InkDisabled;
    public static readonly Vector4 ProcessUntriedHint   = InkMuted;

    // ── Seal palette (active vs inactive) ────────────────────────────────────
    public readonly record struct SealColors(Vector4 Fill, Vector4 Ring, Vector4 Label, Vector4 Glyph, float RingStroke);

    public static readonly SealColors SealActive   = new(SealBgActive, GoldAccent, GoldAccent, GoldAccent, StrokeSealActive);
    public static readonly SealColors SealInactive = new(InsetBg, BorderHairline, InkLabel, InkSecondary, StrokeStandard);

    public static SealColors GetSealColors(bool active) => active ? SealActive : SealInactive;

    // ── Font sizes (px, matching SVG values) ─────────────────────────────────
    public const float FontCaption     = 9f;   // entry numbers, DONE/UNTRIED, hints
    public const float FontOverline    = 10f;  // letter-spaced labels (HABITAT, CATEGORIES)
    public const float FontBody        = 11f;  // body / variant tabs
    public const float FontBodyLg      = 12f;  // category list rows, search input
    public const float FontTitle       = 13f;  // outcome line, habitat value
    public const float FontStageGlyph  = 14f;  // ✓ inside seal
    public const float FontStageRatio  = 11f;  // "2/4" inside Processed seal
    public const float FontOverlineLg  = 12f;  // detail-panel section overlines (HABITAT, PROPERTIES, ...)
    public const float FontBodyLgPlus  = 15f;  // habitat value, process outcome
    public const float FontHeading     = 16f;  // discovery counter total
    public const float FontDisplay     = 18f;  // index title "Forager's index"
    public const float FontDetailName  = 24f;  // detail panel name "Reishi"

    // ── Letter-spacing (custom-drawn text only — ImGui has no native API) ────
    public const float LetterSpacingTitle    = 2.0f;  // "THE ALMANAC · CODEX"
    public const float LetterSpacingOverline = 1.5f;  // section labels
    public const float LetterSpacingSeal     = 1.6f;  // SEEN / HELD / USED — generous breathing room

    // ── Spacing scale ────────────────────────────────────────────────────────
    public const float SpaceXs  = 4f;
    public const float SpaceSm  = 8f;
    public const float SpaceMd  = 12f;
    public const float SpaceLg  = 16f;
    public const float SpaceXl  = 20f;
    public const float SpaceXxl = 28f;

    public const float WindowPadding   = 20f;
    public const float SectionGap      = 16f;
    public const float HairlineDivider = 0.5f;

    // ── Radii ────────────────────────────────────────────────────────────────
    public const float RadiusFrame      = 4f;   // grid cell, process card
    public const float RadiusWindow     = 6f;   // dialog frame
    public const float RadiusVariantTab = 3f;
    public const float RadiusPill       = 13f;  // fully-rounded pill (height 26 → r 13)

    // ── Component dimensions (px) ────────────────────────────────────────────
    public const float TopBarHeight    = 64f;
    public const float SidebarWidth    = 180f;

    public const float GridCellW       = 72f;
    public const float GridCellH       = 80f;
    public const float GridCellGap     = 6f;
    public const float GridCellStride  = 78f;   // 72 + 6 gap

    public const float SealRadius      = 32f;   // outer (bumped from 28 for label breathing room)
    public const float SealInnerRadius = 25f;   // inner ring
    public const float SealBoxSize     = 72f;   // outer bbox (2*r + label space)
    public const float SealStride      = 72f;   // center-to-center spacing

    public const float DetailIconBoxSize = 180f;
    public const float ProcessCardW      = 184f;
    public const float ProcessCardH      = 100f;
    public const float ProcessCardGap    = 10f;
    public const float ProcessCardHeader = 28f;

    public const float ChipHeight    = 26f;
    public const float VariantTabW   = 88f;
    public const float VariantTabH   = 28f;

    // ── Stroke widths ────────────────────────────────────────────────────────
    public const float StrokeHairline   = 0.5f;
    public const float StrokeStandard   = 1.0f;
    public const float StrokeSealActive = 2.0f;
    public const float StrokeInnerRing  = 0.5f;  // inner ring on active seal

    // ── Dashed stroke patterns (for untried / undiscovered) ──────────────────
    public const float DashLength      = 3f;
    public const float DashGap         = 3f;
    public const float DashLengthSmall = 2f;
    public const float DashGapSmall    = 2f;

    // ── Grid stage-dot indicator (bottom of each cell) ──────────────────────
    public const float StageDotRadius      = 3f;
    public const float StageDotStride      = 9f;   // x-spacing between dots
    public const float StageDotEmptyStroke = 0.5f;

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static Vector4 Hex(uint rgb, float a = 1f) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >>  8) & 0xFF) / 255f,
        ( rgb        & 0xFF) / 255f,
        a);

    /// <summary>Pack a <see cref="Vector4"/> color into the ABGR uint that ImDrawList wants.</summary>
    public static uint U(Vector4 v) => ImGui.ColorConvertFloat4ToU32(v);

    public static Vector4 WithAlpha(Vector4 v, float a) => new(v.X, v.Y, v.Z, a);
}
