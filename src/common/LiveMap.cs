using Vintagestory.API.Common;

namespace livemap.common;

public abstract class LiveMap(LiveMapModSystem mod) : IDisposable {
    private readonly LiveMapModSystem _mod = mod;

    protected ILogger Logger => _mod.Mod.Logger;

    public abstract void Dispose();
}
