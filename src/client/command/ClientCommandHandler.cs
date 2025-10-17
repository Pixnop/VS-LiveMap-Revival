using livemap.common.command;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace livemap.client.command;

public class ClientCommandHandler : CommandHandler {
    private readonly LiveMapClient _livemap;

    public ClientCommandHandler(LiveMapClient livemap) {
        _livemap = livemap;

        _livemap.Api.ChatCommands
            .Create("livemap")
            .WithDescription("some simple description")
            .RequiresPrivilege(Privilege.root)
            .HandleWith(LivemapCommand);
    }

    private TextCommandResult LivemapCommand(TextCommandCallingArgs args) {
        return _livemap.OpenAdminDialog() ?
            TextCommandResult.Success("Opening LiveMap admin dialog") :
            TextCommandResult.Error("LiveMap admin dialog already opened");
    }
}
