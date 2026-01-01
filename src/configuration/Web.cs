using livemap.json;
using livemap.tile;
using Newtonsoft.Json;

namespace livemap.configuration;

public class Web {
    public string Path { get; set; } = "web/";

    public string Url { get; set; } = "http://localhost:8080";

    public bool ReadOnly { get; set; } = false;

    [JsonConverter(typeof(TileTypeJsonConverter))]
    public TileType TileType { get; set; } = TileType.Webp;

    public int TileQuality { get; set; } = 100;

    public bool FriendlyUrls { get; set; } = false;

    /// <summary>
    /// Enable 3D map data generation for Three.js frontend
    /// </summary>
    public bool Enable3D { get; set; } = false;

    /// <summary>
    /// Interval in seconds between 3D data regeneration scans
    /// </summary>
    public int Interval3D { get; set; } = 60;
}
