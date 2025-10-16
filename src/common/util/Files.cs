using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Config;

namespace livemap.common.util;

public abstract class Files {
    public static string DataDir { get; private set; } = null!;
    public static string ColormapFile { get; private set; } = null!;
    public static string WebDir { get; private set; } = null!;
    public static string TilesDir { get; private set; } = null!;

    public static void Init(string savegameIdentifier) {
        DataDir = Path.Combine(GamePaths.DataPath, "ModData", savegameIdentifier, "LiveMap");
        ColormapFile = Path.Combine(DataDir, "colormap.json");
        WebDir = Path.Combine(DataDir, "web");
        TilesDir = Path.Combine(WebDir, "tiles");
    }

    public static void SaveAsJson(object obj, string filename) {
        File.WriteAllText(
            Path.Combine(DataDir, filename),
            JsonConvert.SerializeObject(obj),
            Encoding.UTF8
        );
    }
}
