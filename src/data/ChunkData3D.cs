using Newtonsoft.Json;

namespace livemap.data;

/// <summary>
/// Represents 3D terrain data for a single region (512x512 blocks).
/// Used for Three.js rendering on the frontend.
/// </summary>
public class ChunkData3D {
    /// <summary>
    /// Region X coordinate (in region units, not blocks)
    /// </summary>
    [JsonProperty("x")]
    public int RegionX { get; set; }

    /// <summary>
    /// Region Z coordinate (in region units, not blocks)
    /// </summary>
    [JsonProperty("z")]
    public int RegionZ { get; set; }

    /// <summary>
    /// Heightmap data - flattened array of 512x512 height values.
    /// Index = z * 512 + x
    /// </summary>
    [JsonProperty("heightmap")]
    public ushort[] Heightmap { get; set; } = new ushort[512 * 512];

    /// <summary>
    /// Block colors - flattened array of 512x512 RGBA color values (as hex strings).
    /// Index = z * 512 + x
    /// </summary>
    [JsonProperty("colors")]
    public uint[] Colors { get; set; } = new uint[512 * 512];

    /// <summary>
    /// Block IDs for the top surface - useful for texture mapping later.
    /// Index = z * 512 + x
    /// </summary>
    [JsonProperty("blockIds")]
    public int[] BlockIds { get; set; } = new int[512 * 512];

    /// <summary>
    /// Data format version for future compatibility
    /// </summary>
    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Minimum Y value in this region (for optimization)
    /// </summary>
    [JsonProperty("minY")]
    public int MinY { get; set; } = int.MaxValue;

    /// <summary>
    /// Maximum Y value in this region (for optimization)
    /// </summary>
    [JsonProperty("maxY")]
    public int MaxY { get; set; } = int.MinValue;

    /// <summary>
    /// Set data for a specific position in the region
    /// </summary>
    public void SetData(int x, int z, ushort height, uint color, int blockId) {
        int index = (z & 511) * 512 + (x & 511);
        Heightmap[index] = height;
        Colors[index] = color;
        BlockIds[index] = blockId;

        if (height < MinY) MinY = height;
        if (height > MaxY) MaxY = height;
    }

    /// <summary>
    /// Get the array index for a given x, z position
    /// </summary>
    public static int GetIndex(int x, int z) {
        return (z & 511) * 512 + (x & 511);
    }
}
