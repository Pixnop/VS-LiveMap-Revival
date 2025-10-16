using Vintagestory.API.Common;

namespace livemap.common;

public abstract class LiveMap(LiveMapModSystem mod) : IDisposable {
    private readonly LiveMapModSystem _mod = mod;


    public abstract void Dispose();
}
