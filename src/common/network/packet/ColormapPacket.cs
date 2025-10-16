using Vintagestory.API.Server;

namespace livemap.common.network.packet;

public class ColormapPacket {
    public static void ReceivedFromClient(IServerPlayer fromPlayer, ColormapPacket packet) {
        // todo - save player's colormap to disk
        throw new NotImplementedException();
    }

    public static void ReceivedFromServer(ColormapPacket packet) {
        // todo - respond to server's colormap request
        throw new NotImplementedException();
    }
}
