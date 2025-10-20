using ProtoBuf;

namespace livemap.common.configuration;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Config {
    public const string FileName = "LiveMap.json";

#pragma warning disable CA1051, CA1805

    public int InternalWebServer = 8080;

    public string WebPath = "web/";

    public bool ReadOnly = false;

    public string PublicUrl = "http://localhost:8080";

    public string Attribution = "<a href=\"https://mods.vintagestory.at/livemap\" target=\"_blank\">Livemap</a> &copy;2024";

    public string LogoLink = "https://mods.vintagestory.at/livemap";

    public string LogoImg = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path fill='currentColor' d='m2 2 32 16v80l-32-16v-80z'></path><path d='m34 18 32-16 32 16v80l-32-16-32 16'></path><path d='m66 8v68'></path></svg>";

    public string LogoText = "LiveMap";

    public string SiteTitle = "Vintage Story LiveMap";

#pragma warning restore CA1051, CA1805
}
