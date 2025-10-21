using Vintagestory.API.Client;

namespace livemap.client.gui;

public static class Extensions {
    public static GuiComposer AddText(
        this GuiComposer composer,
        string text,
        ElementBounds bounds,
        string? key = null) {
        return composer.AddText(text, CairoFont.WhiteDetailText(), bounds, key);
    }

    public static GuiComposer AddText(
        this GuiComposer composer,
        string text,
        CairoFont font,
        ElementBounds bounds,
        string? key = null) {
        return composer.AddStaticTextAutoBoxSize(text, font, EnumTextOrientation.Center, bounds, key);
    }
}
