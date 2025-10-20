using livemap.client.command;
using livemap.client.gui;
using livemap.client.network;
using livemap.common;
using livemap.common.network.packet;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace livemap.client;

public sealed class LiveMapClient : LiveMap {
    public override ICoreClientAPI Api { get; }

    public ClientCommandHandler CommandHandler { get; }
    public ClientNetworkHandler NetworkHandler { get; }

    private readonly AdminDialog _adminDialog;

    public LiveMapClient(LiveMapModSystem mod, ICoreClientAPI api) : base(mod, api) {
        Api = api;

        _adminDialog = new AdminDialog(this);

        CommandHandler = new ClientCommandHandler(this);
        NetworkHandler = new ClientNetworkHandler(this);
    }

    public bool OpenAdminDialog(AdminDialogPacket? packet = null) {
        if (!Api.World.Player.HasPrivilege(Privilege.root)) {
            return false;
        }

        _adminDialog.Update(packet);

        return _adminDialog.IsOpened() || _adminDialog.TryOpen();
    }

    public override void Dispose() {
        _adminDialog.Dispose();

        NetworkHandler.Dispose();
    }
}
