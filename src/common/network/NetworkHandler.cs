using livemap.common.network.packet;

namespace livemap.common.network;

public abstract class NetworkHandler : IDisposable {
    protected NetworkHandler(LiveMap livemap) {
        livemap.Api.Network.RegisterChannel(livemap.Mod.ModId)
            .RegisterMessageType<ColormapPacket>();
    }

    public abstract void Dispose();
}
