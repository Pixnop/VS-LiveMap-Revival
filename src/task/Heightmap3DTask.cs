using livemap.data;
using livemap.util;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace livemap.task;

/// <summary>
/// Async task that generates 3D heightmap data for the web frontend.
/// Outputs JSON files to web/data/3d/ directory.
/// </summary>
public sealed class Heightmap3DTask : AsyncTask {
    private const int _interval = 60; // seconds between full scans

    private long _lastUpdate;
    private readonly HashSet<ulong> _processedRegions = [];

    public Heightmap3DTask(LiveMap server) : base(server) {
    }

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        // Check if 3D is enabled in config
        if (!_server.Config.Web.Enable3D) {
            return;
        }

        long now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (now - _lastUpdate < _interval) {
            return;
        }

        _lastUpdate = now;

        try {
            await GenerateAllRegions3D(cancellationToken);
        }
        catch (Exception e) {
            Logger.Error($"Error generating 3D data: {e}");
        }
    }

    private async Task GenerateAllRegions3D(CancellationToken cancellationToken) {
        // Get all available region positions
        IEnumerable<ChunkPos> regionPositions = _server.RenderTaskManager?.ChunkLoader
            .GetAllMapRegionPositions() ?? [];

        foreach (ChunkPos regionPos in regionPositions) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            ulong regionKey = ChunkPos.ToChunkIndex(regionPos.X, 0, regionPos.Z);

            // Skip already processed regions (in this session)
            if (_processedRegions.Contains(regionKey)) {
                continue;
            }

            await GenerateRegion3D(regionPos.X, regionPos.Z, cancellationToken);
            _processedRegions.Add(regionKey);
        }
    }

    private async Task GenerateRegion3D(int regionX, int regionZ, CancellationToken cancellationToken) {
        if (_server.RenderTaskManager?.ChunkLoader == null) {
            return;
        }

        ChunkLoader chunkLoader = _server.RenderTaskManager.ChunkLoader;

        // Get region data
        ServerMapRegion? region = chunkLoader.GetMapRegion(ChunkPos.ToChunkIndex(regionX, 0, regionZ));
        if (region == null) {
            return;
        }

        ChunkData3D data3D = new() {
            RegionX = regionX,
            RegionZ = regionZ
        };

        // Get all chunks in this region (16x16 chunks per region)
        int chunkX1 = regionX << 4;
        int chunkZ1 = regionZ << 4;
        int chunkX2 = chunkX1 + 16;
        int chunkZ2 = chunkZ1 + 16;

        IEnumerable<ChunkPos> chunks = chunkLoader.GetAllMapChunkPositions()
            .Where(chunkPos => chunkPos.X >= chunkX1 && chunkPos.Z >= chunkZ1 &&
                              chunkPos.X < chunkX2 && chunkPos.Z < chunkZ2);

        foreach (ChunkPos chunkPos in chunks) {
            if (cancellationToken.IsCancellationRequested) {
                return;
            }

            ScanChunkFor3D(chunkLoader, region, chunkPos, data3D);
        }

        // Write to JSON file
        await WriteRegion3DAsync(data3D, cancellationToken);

        Logger.Debug($"Generated 3D data for region ({regionX}, {regionZ})");
    }

    private void ScanChunkFor3D(ChunkLoader chunkLoader, ServerMapRegion region, ChunkPos chunkPos, ChunkData3D data3D) {
        // Get chunk map data
        ServerMapChunk? mapChunk = chunkLoader.GetMapChunk(ChunkPos.ToChunkIndex(chunkPos.X, chunkPos.Y, chunkPos.Z));
        if (mapChunk == null) {
            return;
        }

        // Skip incomplete chunks
        if (mapChunk.CurrentIncompletePass < EnumWorldGenPass.Done) {
            return;
        }

        // Load the chunk slices needed for surface blocks
        int maxChunkY = _server.Sapi.WorldManager.MapSizeY >> 5;
        ServerChunk?[] chunkSlices = new ServerChunk?[maxChunkY];

        // Determine which chunk slices to load based on heightmap
        HashSet<int> slicesToLoad = [];
        for (int x = 0; x < 32; x++) {
            for (int z = 0; z < 32; z++) {
                int y = GetTopBlockY(mapChunk, x, z);
                slicesToLoad.Add(y >> 5);
            }
        }

        foreach (int sliceY in slicesToLoad) {
            if (sliceY >= 0 && sliceY < maxChunkY) {
                chunkSlices[sliceY] = chunkLoader.GetChunk(ChunkPos.ToChunkIndex(chunkPos.X, sliceY, chunkPos.Z));
            }
        }

        // Scan each block column in the chunk
        int startX = chunkPos.X << 5;
        int startZ = chunkPos.Z << 5;

        for (int localX = 0; localX < 32; localX++) {
            for (int localZ = 0; localZ < 32; localZ++) {
                int worldX = startX + localX;
                int worldZ = startZ + localZ;
                int regionLocalX = worldX & 511;
                int regionLocalZ = worldZ & 511;

                // Get height from heightmap
                ushort height = (ushort)GetTopBlockY(mapChunk, localX, localZ);

                // Get block ID at surface
                int blockId = 0;
                int sliceIndex = height >> 5;
                if (sliceIndex >= 0 && sliceIndex < chunkSlices.Length && chunkSlices[sliceIndex] != null) {
                    int blockIndex = Mathf.BlockIndex(localX, height, localZ);
                    blockId = chunkSlices[sliceIndex]!.Data[blockIndex];
                }

                // Get color from colormap
                uint color = 0;
                if (LiveMap.Api.Colormap.TryGet(blockId, out uint[]? colors)) {
                    color = colors[GameMath.MurmurHash3Mod(worldX, height, worldZ, colors.Length)];
                }

                data3D.SetData(regionLocalX, regionLocalZ, height, color, blockId);
            }
        }
    }

    private int GetTopBlockY(ServerMapChunk mapChunk, int x, int z) {
        ushort blockY = mapChunk.RainHeightMap[Mathf.BlockIndex(x, z)];
        return GameMath.Clamp(blockY, 0, _server.Sapi.WorldManager.MapSizeY - 1);
    }

    private static async Task WriteRegion3DAsync(ChunkData3D data, CancellationToken cancellationToken) {
        string dir3D = Path.Combine(Files.JsonDir, "3d");
        GamePaths.EnsurePathExists(dir3D);

        string fileName = $"region_{data.RegionX}_{data.RegionZ}.json";
        string filePath = Path.Combine(dir3D, fileName);

        string json = JsonConvert.SerializeObject(data, Files.JsonSerializerMinifiedSettings);

        if (cancellationToken.IsCancellationRequested) {
            return;
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    /// <summary>
    /// Force regeneration of a specific region
    /// </summary>
    public void InvalidateRegion(int regionX, int regionZ) {
        ulong regionKey = ChunkPos.ToChunkIndex(regionX, 0, regionZ);
        _processedRegions.Remove(regionKey);
    }

    /// <summary>
    /// Force regeneration of all regions
    /// </summary>
    public void InvalidateAll() {
        _processedRegions.Clear();
    }
}
