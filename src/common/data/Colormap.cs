using System.Text;
using livemap.common.network.packet;
using livemap.common.util;
using livemap.server;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace livemap.common.data;

public sealed class Colormap(LiveMapServer livemap) : IDisposable {
    public const int ColorsPerBlock = 30;

    private readonly Dictionary<string, uint[]> _colorsByName = [];
    private readonly Dictionary<int, uint[]> _colorsById = [];

    /// <summary>
    /// Add a color to the colormap. This should only be called by the client when building the colormap.
    /// </summary>
    /// <param name="code">Block's code id</param>
    /// <param name="colors">Possible colors this block can be</param>
    public void Add(string code, uint[] colors) {
        _colorsByName.TryAdd(code, colors);
    }

    public uint[]? Get(int id) {
        return _colorsById.GetValueOrDefault(id);
    }

    public string Serialize() {
        return JsonConvert.SerializeObject(_colorsByName);
    }

    public void LoadFromDisk(string path) {
        new Thread(() => {
            Logger.Event("Loading colormap from disk");

            if (File.Exists(path)) {
                if (LoadFromJson(File.ReadAllText(path, Encoding.UTF8))) {
                    Logger.Event("Colormap loaded from disk");
                    return;
                }
            } else {
                Logger.Warning("No colormap file found");
            }

            Logger.Warning("Colormap could not be loaded from disk");
            Logger.Warning("An admin needs to send the colormap from their client.");
        }).Start();
    }

    public void LoadFromPacket(ColormapPacket packet) {
        new Thread(() => {
            Logger.Event("Loading colormap from packet");

            string json = ""; //packet.Decompress().JsonValue;

            if (LoadFromJson(json)) {
                Files.SaveAsJson(_colorsByName, Files.ColormapFile);
                RefreshIds();
                Logger.Event("Colormap saved to disk");
            } else {
                Logger.Warning("Could not save colormap to disk");
            }
        }).Start();
    }

    public bool LoadFromJson(string? json) {
        _colorsByName.Clear();
        _colorsById.Clear();

        if (string.IsNullOrEmpty(json)) {
            Logger.Warning("Colormap data seems to be empty");
            return false;
        }

        try {
            JsonConvert.DeserializeObject<Dictionary<string, uint[]>>(json)?
                .Foreach(pair => Add(pair.Key, pair.Value));
            RefreshIds();
            return true;
        } catch (Exception e) {
            Logger.Error(e.ToString());
            return false;
        }
    }

    private void RefreshIds() {
        _colorsById.Clear();

        foreach ((string code, uint[] colors) in _colorsByName) {
            Block block = livemap.Api.World.GetBlock(new AssetLocation(code));
            if (block == null) {
                Logger.Warning($"Invalid or unknown block id in colormap ({code})");
                continue;
            }

            // ensure opaque alpha channel
            for (int i = 0; i < colors.Length; i++) {
                if (colors[i] > 0) {
                    colors[i] |= (uint)0xFF << 24;
                }
            }

            _colorsById.TryAdd(block.Id, colors);
        }
    }

    public void Dispose() {
        _colorsByName.Clear();
        _colorsById.Clear();
    }
}
