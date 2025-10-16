using livemap.common.network;
using livemap.common.network.packet;
using Vintagestory.API.Server;

namespace livemap.server.network;

public sealed class ServerNetworkHandler(LiveMapServer livemap) : NetworkHandler(livemap) {
    private IServerNetworkChannel? _channel = livemap.Api.Network.GetChannel(livemap.Mod.ModId)?
        .SetMessageHandler<ColormapPacket>(ColormapPacket.ReceivedFromClient);

    public void SendPacket<T>(T packet, IServerPlayer? recipient = null) {
        _channel?.SendPacket(packet, recipient);
    }

    public override void Dispose() {
        _channel = null;
    }
}
