using livemap.common.network;
using livemap.common.network.packet;
using Vintagestory.API.Client;

namespace livemap.client.network;

public sealed class ClientNetworkHandler(LiveMapClient livemap) : NetworkHandler(livemap) {
    private IClientNetworkChannel? _channel = livemap.Api.Network.GetChannel(livemap.Mod.ModId)?
        .SetMessageHandler<ColormapPacket>(ColormapPacket.ReceivedFromServer);

    public void SendPacket<T>(T packet) {
        _channel?.SendPacket(packet);
    }

    public override void Dispose() {
        _channel = null;
    }
}
