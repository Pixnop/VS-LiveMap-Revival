using livemap.common.util;
using Vintagestory.API.Common;

namespace livemap.common;

public abstract class LiveMap : IDisposable {
    public LiveMapModSystem Mod { get; }
    public abstract ICoreAPI Api { get; }

    protected LiveMap(LiveMapModSystem mod, ICoreAPI api) {
        Mod = mod;
        Files.Init(api.World.SavegameIdentifier);
    }

    public abstract void Dispose();
}
