using JetBrains.Annotations;
using livemap.client;
using livemap.common;
using livemap.server;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace livemap;

public sealed class LiveMapModSystem : ModSystem, IDisposable {
    private static LiveMapModSystem _instance = null!;
    public static ILogger Logger => _instance.Mod.Logger;

    private LiveMap? _livemap;

    public LiveMapModSystem() {
        _instance = this;
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
