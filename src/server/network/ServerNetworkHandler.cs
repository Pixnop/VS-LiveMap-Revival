using livemap.common.network;
using livemap.common.network.packet;
using livemap.common.util;
using Vintagestory.API.Server;

namespace livemap.server.network;

public sealed class ServerNetworkHandler : NetworkHandler {
    private readonly LiveMapServer _livemap;

    private IServerNetworkChannel? _channel;

    public ServerNetworkHandler(LiveMapServer livemap) : base(livemap) {
        _livemap = livemap;
        _channel = livemap.Api.Network.GetChannel(livemap.Mod.ModId)?
            .SetMessageHandler<AdminDialogPacket>(ReceivedAdminDialogPacket)
            .SetMessageHandler<ColormapPacket>(ReceivedColormapPacket);
    }

    private void ReceivedAdminDialogPacket(IServerPlayer sender, AdminDialogPacket packet) {
        Logger.Event($"Received admin dialog request from {sender.PlayerName}");

        SendAdminPacket(sender);
    }

    private void ReceivedColormapPacket(IServerPlayer sender, ColormapPacket packet) {
        Logger.Event($"Received colormap request from {sender.PlayerName}");

        // todo - respond to server's colormap request
        throw new NotImplementedException();
    }

    public void SendAdminPacket(IServerPlayer player) {
        if (!player.HasPrivilege(Privilege.root)) {
            return;
        }

        AdminDialogPacket packet = new() {
            Config = _livemap.Config,
            ColormapSize = _livemap.Colormap.Size
        };

        SendPacket(packet, player);
    }

    public void SendPacket<T>(T packet, IServerPlayer? recipient = null) where T : Packet {
        _channel?.SendPacket(packet, recipient);
    }

    public override void Dispose() {
        _channel = null;
    }
}
