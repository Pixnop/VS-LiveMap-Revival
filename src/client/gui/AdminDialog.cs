using Vintagestory.API.Client;

namespace livemap.client.gui;

public class AdminDialog : GuiDialog {
    public override string? ToggleKeyCombinationCode => null;

    public AdminDialog(ICoreClientAPI api) : base(api) {
        SetupDialog();
    }

    private void SetupDialog() {
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        ElementBounds textBounds = ElementBounds.Fixed(0, 40, 300, 100);

        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren);

        SingleComposer = capi.Gui.CreateCompo("livemap:admin_dialog", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("LiveMap - Admin Dialog", () => TryClose())
            .BeginChildElements(bgBounds)
            .AddStaticText("This is a piece of text at the center of your screen - Enjoy!", CairoFont.WhiteDetailText(), textBounds)
            .EndChildElements()
            .Compose();
    }
}
