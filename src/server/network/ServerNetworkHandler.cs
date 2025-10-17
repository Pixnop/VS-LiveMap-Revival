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

    private void ReceivedAdminDialogPacket(IServerPlayer fromPlayer, AdminDialogPacket packet) {
        throw new NotImplementedException();
    }

    private void ReceivedColormapPacket(IServerPlayer fromPlayer, ColormapPacket packet) {
        Logger.Event("Received colormap request from server");

        // todo - respond to server's colormap request
        throw new NotImplementedException();
    }

    public void SendPacket<T>(T packet, IServerPlayer? recipient = null) where T : Packet {
        _channel?.SendPacket(packet, recipient);
    }

    public override void Dispose() {
        _channel = null;
    }
}
