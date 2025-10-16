using livemap.common;
using Vintagestory.API.Client;

namespace livemap.client;

public sealed class LiveMapClient(LiveMapModSystem mod, ICoreClientAPI api) : LiveMap(mod, api) {
    public override ICoreClientAPI Api => api;

    public override void Dispose() {
        // TODO release managed resources here
    }
}
