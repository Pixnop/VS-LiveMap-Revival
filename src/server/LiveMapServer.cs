using livemap.common;
using livemap.common.configuration;
using livemap.common.util;
using Vintagestory.API.Server;

namespace livemap.server;

public sealed class LiveMapServer : LiveMap {
    public override ICoreServerAPI Api { get; }

    public Config Config { get; private set; } = null!;

    public LiveMapServer(LiveMapModSystem mod, ICoreServerAPI api) : base(mod, api) {
        Api = api;

        ReloadConfig();
    }

    public void ReloadConfig() {
        Logger.Event("Loading config from disk...");
        Config = Api.LoadModConfig<Config>(Config.FileName) ?? new Config();
        SaveConfig();
    }

    public void SaveConfig() {
        Logger.Event("Saving config to disk...");
        Api.StoreModConfig(Config, Config.FileName);
    }

    public override void Dispose() {
    }
}
