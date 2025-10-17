using livemap.client.command;
using livemap.client.gui;
using livemap.client.network;
using livemap.common;
using Vintagestory.API.Client;

namespace livemap.client;

public sealed class LiveMapClient : LiveMap {
    public override ICoreClientAPI Api { get; }

    public ClientCommandHandler CommandHandler { get; }
    public ClientNetworkHandler NetworkHandler { get; }

    private readonly AdminDialog _adminDialog;

    public LiveMapClient(LiveMapModSystem mod, ICoreClientAPI api) : base(mod, api) {
        Api = api;

        _adminDialog = new AdminDialog(Api);

        CommandHandler = new ClientCommandHandler(this);
        NetworkHandler = new ClientNetworkHandler(this);
    }

    public bool OpenAdminDialog() {
        return !_adminDialog.IsOpened() && _adminDialog.TryOpen();
    }

    public override void Dispose() {
        _adminDialog.Dispose();

        NetworkHandler.Dispose();
    }
}
