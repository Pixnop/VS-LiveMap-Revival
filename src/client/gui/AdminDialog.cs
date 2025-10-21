using livemap.common.network.packet;
using livemap.common.util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace livemap.client.gui;

public class AdminDialog(LiveMapClient livemap) : GuiDialog(livemap.Api) {
    private readonly LiveMapClient _livemap = livemap;

    private readonly AnimatedGif _loadingGif = new(livemap.Api, livemap.Api.Assets.Get(new AssetLocation(livemap.Mod.ModId, "textures/icons/spinner.gif")).Data);

    private int _colormapSize = -1;

    public override string? ToggleKeyCombinationCode => null;

    public void Update(AdminDialogPacket? packet = null) {
        if (packet == null) {
            if (IsOpened()) {
                return;
            }
            _livemap.NetworkHandler.SendPacket(new AdminDialogPacket());
            Dialog(ComposeLoading);
            SingleComposer.GetButton("button-colormap").Enabled = false;
            _colormapSize = -1;
        } else {
            // todo - populate data on screen from packet
            _colormapSize = packet.ColormapSize;
            Dialog(Compose);
        }
    }

    private void Compose() {
        ElementBounds bounds = ElementBounds.Fixed(0, 0);

        // todo - use lang file
        SingleComposer.AddText($"Colormap: {_colormapSize}", bounds);
        SingleComposer.AddText($"Known Blocks: {0 + 0}", bounds = bounds.BelowCopy());
        SingleComposer.AddText($"Another Thing: {0 + 0}", bounds = bounds.BelowCopy());
        SingleComposer.AddText($"And Another: {0 + 0}", bounds = bounds.BelowCopy());
        SingleComposer.AddText($"Yet Another One: {0 + 0}", bounds = bounds.BelowCopy());
    }

    private void ComposeLoading() {
        _loadingGif.Bounds = ElementBounds
            .FixedSize(EnumDialogArea.CenterMiddle, 64, 64)
            .WithFixedOffset(0, -40);

        ElementBounds bounds = ElementBounds.Empty
            .WithAlignment(EnumDialogArea.CenterMiddle);

        SingleComposer.AddInteractiveElement(_loadingGif)
            .AddText(Lang.Get("loading"), bounds);
    }

    private void Dialog(Action composeContent) {
        ElementBounds contents = ElementBounds
            .Fixed(0, GuiStyle.TitleBarHeight, 500, 500);

        ElementBounds closeBtn = ElementBounds
            .FixedSize(0.0, 0.0)
            .FixedUnder(contents, 18.0)
            .WithAlignment(EnumDialogArea.RightFixed)
            .WithFixedPadding(20.0, 4.0)
            .WithFixedAlignmentOffset(2.0, 0.0);

        ElementBounds colormapBtn = closeBtn.FlatCopy()
            .WithAlignment(EnumDialogArea.LeftFixed)
            .WithFixedAlignmentOffset(-2.0, 0.0);

        ElementBounds dialogBg = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(contents, closeBtn, colormapBtn);

        SingleComposer = capi.Gui.CreateCompo("livemap-admin-dialog", ElementStdBounds.AutosizedMainDialog)
            .AddShadedDialogBG(dialogBg)
            .AddDialogTitleBar(Lang.Get("admin-dialog-title"), OnTitleBarClose)
            .BeginChildElements(contents);

        composeContent.Invoke();

        SingleComposer
            .EndChildElements()
            .AddSmallButton(Lang.Get("button-colormap"), OnButtonColormap, colormapBtn, key: "button-colormap")
            .AddSmallButton(Lang.Get("button-close"), OnButtonClose, closeBtn)
            .Compose();
    }

    private bool OnButtonColormap() {
        Logger.Event("Colormap Button Clicked");
        return true;
    }

    private bool OnButtonClose() {
        Logger.Event("Close Button Clicked");
        TryClose();
        return true;
    }

    private void OnTitleBarClose() {
        Logger.Event("Titlebar Close Clicked");
    }
}
