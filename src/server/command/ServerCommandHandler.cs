using livemap.common.command;
using livemap.common.network.packet;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace livemap.server.command;

public class ServerCommandHandler : CommandHandler {
    private readonly LiveMapServer _livemap;

    public ServerCommandHandler(LiveMapServer livemap) {
        _livemap = livemap;

        _livemap.Api.ChatCommands
            .Create("livemap")
            .WithDescription("some simple description")
            .RequiresPrivilege(Privilege.root)
            .HandleWith(LivemapCommand);
    }

    private TextCommandResult LivemapCommand(TextCommandCallingArgs args) {
        if (args.Caller.Player is not IServerPlayer player) {
            return TextCommandResult.Error("Player only command");
        }

        _livemap.NetworkHandler.SendAdminPacket(player);

        return TextCommandResult.Success("Opening LiveMap admin dialog");
    }
}
