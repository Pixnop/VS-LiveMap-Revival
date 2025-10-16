using livemap.common;
using livemap.common.configuration;
using livemap.common.data;
using livemap.common.util;
using Vintagestory.API.Server;

namespace livemap.server;

public sealed class LiveMapServer : LiveMap {
    public override ICoreServerAPI Api { get; }

    public Config Config { get; private set; } = null!;

    public Colormap Colormap { get; }

    public LiveMapServer(LiveMapModSystem mod, ICoreServerAPI api) : base(mod, api) {
        Api = api;

        ReloadConfig();

        Colormap = new Colormap(this);

        Api.Event.RegisterCallback(_ => RunOnFirstTick(), 1);
    }

    private void RunOnFirstTick() {
        Colormap.LoadFromDisk(Files.ColormapFile);
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
        Colormap.Dispose();
    }
}
