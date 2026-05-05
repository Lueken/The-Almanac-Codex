using Vintagestory.API.Common;

namespace AlmanacCodex;

public static class CodexLogger
{
    private const string Prefix = "almanac:codex";

    public static void Info(ICoreAPI api, string component, string message)
        => api.Logger.Notification($"[{Prefix}:{component}] {message}");

    public static void Debug(ICoreAPI api, string component, string message)
        => api.Logger.Debug($"[{Prefix}:{component}] {message}");

    public static void Warn(ICoreAPI api, string component, string message)
        => api.Logger.Warning($"[{Prefix}:{component}] {message}");

    public static void Error(ICoreAPI api, string component, string message)
        => api.Logger.Error($"[{Prefix}:{component}] {message}");
}
