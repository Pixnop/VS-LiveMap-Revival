using System.Text;
using livemap.common.network.packet;
using livemap.common.util;
using Newtonsoft.Json;
using Vintagestory.API.Util;

namespace livemap.common.data;

public sealed class Colormap : IDisposable {
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

    public bool Deserialize(string? json) {
        _colorsByName.Clear();
        _colorsById.Clear();

        if (string.IsNullOrEmpty(json)) {
            Logger.Error("Colormap seems to be empty");
            return false;
        }

        try {
            JsonConvert.DeserializeObject<Dictionary<string, uint[]>>(json)?
                .Foreach(pair => Add(pair.Key, pair.Value));
            return true;
        }
        catch (Exception e) {
            Logger.Error(e.ToString());
            return false;
        }
    }

    public void LoadFromPacket(ColormapPacket packet) {
    }

    public void LoadFromDisk() {
        new Thread(() => {
            string? json = null;
            if (File.Exists(Files.ColormapFile)) {
                json = File.ReadAllText(Files.ColormapFile, Encoding.UTF8);
            }

            if (Deserialize(json)) {
                Logger.Event("Colormap loaded from disk");
            }
            else {
                Logger.Error("Colormap could not be loaded from disk");
            }
        }).Start();
    }

    public void SaveToDisk() {
        Files.SaveAsJson(_colorsByName, Files.ColormapFile);
    }

    public void Dispose() {
        _colorsByName.Clear();
        _colorsById.Clear();
    }
}
