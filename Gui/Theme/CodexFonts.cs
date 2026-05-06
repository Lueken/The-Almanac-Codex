using System;
using System.Collections.Generic;
using System.IO;
using ImGuiNET;
using Vintagestory.API.Client;
using VSImGui;

namespace AlmanacCodex.Gui.Theme;

/// <summary>
/// Registers a serif font with vsimgui's FontManager and exposes a size → ImFontPtr lookup
/// matching the typographic scale in <see cref="CodexTheme"/>. Falls back to the default
/// ImGui font if no serif is available on the system (the SVG was designed against Georgia,
/// which ships on Windows + macOS; Linux falls back to DejaVu Serif).
///
/// Usage:
///   - <see cref="Register"/> once on client startup
///   - <see cref="Get"/>(size) at draw time to obtain an ImFontPtr to push
/// </summary>
public static class CodexFonts
{
    public static readonly int[] Sizes = { 9, 10, 11, 12, 13, 14, 15, 16, 18, 24 };

    private static string? resolvedFontPath;
    private static bool registered;
    private static readonly Dictionary<int, ImFontPtr> cache = new();
    private static bool atlasScanned;

    public static bool HasSerif => resolvedFontPath != null;

    public static void Register(ICoreClientAPI capi)
    {
        if (registered) return;

        resolvedFontPath = ResolveFontPath();
        if (resolvedFontPath == null)
        {
            CodexLogger.Warn(capi, "fonts",
                "no serif font found on system; Codex will use the default ImGui font");
            registered = true;
            return;
        }

        try
        {
            VSImGui.API.FontManager.BeforeFontsLoaded += OnBeforeFontsLoaded;
            CodexLogger.Info(capi, "fonts",
                $"hooked BeforeFontsLoaded; will register '{Path.GetFileName(resolvedFontPath)}' " +
                $"at sizes [{string.Join(",", Sizes)}]");
        }
        catch (Exception ex)
        {
            CodexLogger.Warn(capi, "fonts",
                $"failed to subscribe to FontManager.BeforeFontsLoaded ({ex.GetType().Name}: {ex.Message}); default font in use");
            resolvedFontPath = null;
        }

        registered = true;
    }

    private static void OnBeforeFontsLoaded(HashSet<string> fonts, HashSet<int> sizes)
    {
        if (resolvedFontPath != null) fonts.Add(resolvedFontPath);
        foreach (var s in Sizes) sizes.Add(s);
    }

    public static ImFontPtr Get(int size)
    {
        if (resolvedFontPath == null) return ImGui.GetIO().FontDefault;

        if (!atlasScanned)
        {
            ScanAtlas();
            atlasScanned = true;
        }

        return cache.TryGetValue(size, out var ptr) ? ptr : ImGui.GetIO().FontDefault;
    }

    private static void ScanAtlas()
    {
        var atlas = ImGui.GetIO().Fonts;
        // Walk in load order. vsimgui's default font (ProggyClean / similar bitmap) loads first,
        // followed by any font paths registered via FontManager. Keying by FontSize and taking
        // the LAST font seen at each size guarantees our serif wins over the default at
        // overlapping sizes (e.g. 13).
        for (int i = 0; i < atlas.Fonts.Size; i++)
        {
            var f = atlas.Fonts[i];
            int sizeKey = (int)Math.Round(f.FontSize);
            cache[sizeKey] = f;
        }
    }

    private static string? ResolveFontPath()
    {
        // Order: Windows Georgia → macOS Georgia → Linux DejaVu Serif fallback.
        // TODO(#15-ish): bundle EB Garamond in assets/almanaccodex/fonts/ for cross-platform parity.
        var candidates = new[]
        {
            @"C:\Windows\Fonts\georgia.ttf",
            @"C:\Windows\Fonts\Georgia.ttf",
            "/Library/Fonts/Georgia.ttf",
            "/System/Library/Fonts/Supplemental/Georgia.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
            "/usr/share/fonts/dejavu/DejaVuSerif.ttf",
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
