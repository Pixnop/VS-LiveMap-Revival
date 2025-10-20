using livemap.common.configuration;
using ProtoBuf;

namespace livemap.common.network.packet;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AdminDialogPacket : Packet {
    public int ColormapSize;

    public Config? Config;
}
