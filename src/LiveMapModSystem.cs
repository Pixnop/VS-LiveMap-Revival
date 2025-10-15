using JetBrains.Annotations;
using livemap.client;
using livemap.common;
using livemap.server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace livemap;

[UsedImplicitly]
public sealed class LiveMapModSystem : ModSystem {
    private LiveMap? _livemap;

    public override void StartClientSide(ICoreClientAPI api) {
        _livemap = new LiveMapClient(this, api);
    }

    public override void StartServerSide(ICoreServerAPI api) {
        _livemap = new LiveMapServer(this, api);
    }

    public override void Dispose() {
        _livemap?.Dispose();
        _livemap = null;
    }
}
