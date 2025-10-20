using livemap.common.network;
using livemap.common.network.packet;
using livemap.common.util;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace livemap.client.network;

public sealed class ClientNetworkHandler : NetworkHandler {
    private readonly LiveMapClient _livemap;

    private IClientNetworkChannel? _channel;

    public ClientNetworkHandler(LiveMapClient livemap) : base(livemap) {
        _livemap = livemap;
        _channel = livemap.Api.Network.GetChannel(livemap.Mod.ModId)?
            .SetMessageHandler<AdminDialogPacket>(ReceivedAdminDialogPacket)
            .SetMessageHandler<ColormapPacket>(ReceivedColormapPacket);
    }

    private void ReceivedAdminDialogPacket(AdminDialogPacket packet) {
        Logger.Event("Received admin dialog request from server");

        _livemap.OpenAdminDialog(packet);
    }

    private void ReceivedColormapPacket(ColormapPacket packet) {
        Logger.Event("Received colormap request from server");

        // todo - respond to server's colormap request
        throw new NotImplementedException();
    }

    public void SendPacket<T>(T packet) where T : Packet {
        _channel?.SendPacket(packet);
    }

    public override void Dispose() {
        _channel = null;
    }
}
