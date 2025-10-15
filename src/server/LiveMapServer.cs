using livemap.common;
using livemap.common.configuration;
using Vintagestory.API.Server;

namespace livemap.server;

public sealed class LiveMapServer(LiveMapModSystem mod, ICoreServerAPI api) : LiveMap(mod) {
    private readonly ICoreServerAPI _api = api;

    private Config? _config;

    public Config Config => _config ?? ReloadConfig();

    public Config ReloadConfig() {
        Logger.Event("Loading config from disk...");
        _config = _api.LoadModConfig<Config>(Config.FileName) ?? new Config();
        _api.StoreModConfig(_config, Config.FileName);
        return _config;
    }

    public override void Dispose() {
        _config = null;
    }
}
