using livemap.client;
using livemap.common;
using livemap.server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace livemap;

public sealed class LiveMapModSystem : ModSystem, IDisposable {
    private static LiveMapModSystem? _instance;
    public static ILogger? Logger => _instance?._api?.Logger;

    private ICoreAPI? _api;
    private LiveMap? _livemap;

    public string ModId => Mod.Info.ModID;

    public LiveMapModSystem() {
        _instance = this;
    }

    public override void StartPre(ICoreAPI api) {
        _api = api;
    }

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
