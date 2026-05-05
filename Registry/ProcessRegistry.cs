using System.Collections.Generic;
using Vintagestory.API.Common;

namespace AlmanacCodex.Registry;

public class ProcessRegistry
{
    private readonly Dictionary<string, ProcessDefinition> processes = new();
    private readonly ICoreAPI api;

    public ProcessRegistry(ICoreAPI api)
    {
        this.api = api;
    }

    public void Register(ProcessDefinition def)
    {
        if (processes.TryGetValue(def.Code, out var existing))
        {
            CodexLogger.Warn(api, "process-registry",
                $"duplicate registration for '{def.Code}': previously claimed by '{existing.OwnerModId}', now also claimed by '{def.OwnerModId}'. Keeping the first.");
            return;
        }
        processes[def.Code] = def;
        CodexLogger.Info(api, "process-registry",
            $"registered process '{def.Code}' (owner='{def.OwnerModId}', display='{def.DisplayKey}')");
    }

    public ProcessDefinition? Get(string code) => processes.TryGetValue(code, out var def) ? def : null;

    public IReadOnlyCollection<ProcessDefinition> All => processes.Values;

    public bool IsRegistered(string code) => processes.ContainsKey(code);
}
