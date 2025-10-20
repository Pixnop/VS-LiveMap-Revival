using livemap.common.network.packet;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace livemap.client.gui;

public class AdminDialog(LiveMapClient livemap) : GuiDialog(livemap.Api) {
    private readonly LiveMapClient _livemap = livemap;

    private readonly AnimatedGif _loadingGif = new(livemap.Api, livemap.Api.Assets.Get(new AssetLocation(livemap.Mod.ModId, "textures/icons/spinner.gif")).Data);

    private int _colormapSize = -1;

    public override string? ToggleKeyCombinationCode => null;

    private void Compose(bool loading = false) {
        ElementBounds bgBounds = ElementBounds
            .FixedSize(600, 400)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding);

        SingleComposer = capi.Gui.CreateCompo("livemap:admin_dialog", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("LiveMap - Admin Dialog", () => TryClose())
            .BeginChildElements(bgBounds)
            .AddStaticTextAutoBoxSize($"Colormap Size: {_colormapSize}", CairoFont.WhiteDetailText(), EnumTextOrientation.Center, ElementBounds.Fixed(0, 40));

        if (loading) {
            _loadingGif.Bounds = ElementBounds
                .FixedSize(64, 64)
                .WithAlignment(EnumDialogArea.CenterMiddle);
            SingleComposer.AddInteractiveElement(_loadingGif)
                .AddStaticTextAutoBoxSize("Loading...", CairoFont.WhiteDetailText(), 0,
                    ElementBounds.Fixed(0, 50)
                        .WithAlignment(EnumDialogArea.CenterMiddle)
                );
        }

        SingleComposer.EndChildElements().Compose();
    }

    public void Update(AdminDialogPacket? packet = null) {
        if (packet == null) {
            _livemap.NetworkHandler.SendPacket(new AdminDialogPacket());
            Compose(true);
        } else {
            // todo - populate data on screen
            _colormapSize = packet.ColormapSize;
            Compose();
        }
    }
}
