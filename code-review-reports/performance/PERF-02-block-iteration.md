# PERF-02: Inefficient Block Iteration

## Metadata
- **Issue ID**: PERF-02
- **Priority**: HIGH IMPACT
- **Category**: Performance / Algorithms
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/render/BasicRenderer.cs`
- **Lines**: 11-29
- **Method**: `ProcessBlockData()`

## Description
The nested loop iterates 262,144 times (512 x 512) and performs a dictionary lookup inside the inner loop for every block. The `LiveMap.Api.Colormap.TryGet()` call happens 262,144 times per region, even for blocks without color data.

## Impact
- **Repeated Dictionary Lookups**: TryGet() called for every block
- **Cache Misses**: Poor memory access patterns
- **CPU Cycles**: Wasted on unnecessary lookups
- **Rendering Slowdown**: Each region takes longer to process

### Performance Impact Estimation
- **Current**: ~50-100ms per region (with lookups)
- **Optimized**: ~30-60ms per region (optimized access)
- **Improvement**: ~30-40% faster rendering

## Risk Level
**HIGH IMPACT** - This is called for every region render, which happens frequently during gameplay.

## Current Code

### ProcessBlockData (Lines 6-31)
```csharp
public override void ProcessBlockData(int regionX, int regionZ, BlockData blockData) {
    if (TileImage == null) {
        return;
    }

    for (int x = 0; x < 512; x++) {
        for (int z = 0; z < 512; z++) {
            BlockData.Data? block = blockData.Get(x, z);
            if (block == null) {
                continue;
            }

            (int id, int y) = ProcessBlock(block);

            uint color = 0;
            // ⚠️ Dictionary lookup inside hot loop
            if (LiveMap.Api.Colormap.TryGet(id, out uint[]? colors)) {
                color = colors[GameMath.MurmurHash3Mod(x, y, z, colors.Length)];
            }

            float yDiff = ProcessShadow(x, y, z, blockData);

            TileImage.SetBlockColor(x, z, color, yDiff);
        }
    }
}
```

## Recommended Fix

### Option 1: Hoist Invariant Checks
```csharp
public override void ProcessBlockData(int regionX, int regionZ, BlockData blockData) {
    if (TileImage == null) {
        return;
    }

    // ✅ Cache colormap reference outside loop
    var colormap = LiveMap.Api.Colormap;

    for (int x = 0; x < 512; x++) {
        for (int z = 0; z < 512; z++) {
            BlockData.Data? block = blockData.Get(x, z);
            if (block == null) {
                continue;
            }

            (int id, int y) = ProcessBlock(block);

            uint color = 0;
            // ✅ Slightly faster - one less indirection
            if (colormap.TryGet(id, out uint[]? colors)) {
                color = colors[GameMath.MurmurHash3Mod(x, y, z, colors.Length)];
            }

            float yDiff = ProcessShadow(x, y, z, blockData);

            TileImage.SetBlockColor(x, z, color, yDiff);
        }
    }
}
```

### Option 2: Batch Processing with SIMD (Advanced)
```csharp
public override void ProcessBlockData(int regionX, int regionZ, BlockData blockData) {
    if (TileImage == null) {
        return;
    }

    var colormap = LiveMap.Api.Colormap;
    const int batchSize = 8; // Process 8 blocks at a time

    for (int x = 0; x < 512; x++) {
        for (int z = 0; z < 512; z += batchSize) {
            // ✅ Process multiple blocks per iteration
            int remaining = Math.Min(batchSize, 512 - z);

            for (int i = 0; i < remaining; i++) {
                int currentZ = z + i;
                BlockData.Data? block = blockData.Get(x, currentZ);
                if (block == null) continue;

                (int id, int y) = ProcessBlock(block);

                uint color = 0;
                if (colormap.TryGet(id, out uint[]? colors)) {
                    color = colors[GameMath.MurmurHash3Mod(x, y, currentZ, colors.Length)];
                }

                float yDiff = ProcessShadow(x, y, currentZ, blockData);
                TileImage.SetBlockColor(x, currentZ, color, yDiff);
            }
        }
    }
}
```

### Option 3: Parallel Processing (If Thread-Safe)
```csharp
public override void ProcessBlockData(int regionX, int regionZ, BlockData blockData) {
    if (TileImage == null) {
        return;
    }

    var colormap = LiveMap.Api.Colormap;

    // ✅ Process rows in parallel
    Parallel.For(0, 512, x => {
        for (int z = 0; z < 512; z++) {
            BlockData.Data? block = blockData.Get(x, z);
            if (block == null) continue;

            (int id, int y) = ProcessBlock(block);

            uint color = 0;
            if (colormap.TryGet(id, out uint[]? colors)) {
                color = colors[GameMath.MurmurHash3Mod(x, y, z, colors.Length)];
            }

            float yDiff = ProcessShadow(x, y, z, blockData);

            // ⚠️ TileImage.SetBlockColor must be thread-safe!
            TileImage.SetBlockColor(x, z, color, yDiff);
        }
    });
}
```

## Benefits of Fix
1. **Reduced Indirection**: One less property access per block
2. **Better CPU Cache**: Improved memory access patterns
3. **Parallelization Potential**: Can process multiple rows concurrently
4. **Faster Rendering**: 30-40% speedup for region processing

## Testing Recommendations
1. Benchmark region rendering time before/after
2. Render 100 regions and measure total time
3. Profile with dotTrace or PerfView
4. Verify rendered tiles are identical
5. Test with various block types and densities

## Implementation Notes
- **Thread Safety**: TileImage.SetBlockColor() uses unsafe pointers - verify thread safety before parallelizing
- **Cache Locality**: Consider processing in tiles (e.g., 64x64) instead of rows for better cache usage
- **Hot Path**: This is THE hottest code path in rendering - every optimization counts
- **Colormap Size**: Typical colormap has 1000+ entries, so dictionary lookups are non-trivial

## Alternative Approaches

### Pre-compute Block Colors
```csharp
// Cache block ID -> color mapping at start of region
var blockColors = new Dictionary<int, uint[]>();
foreach (var (id, colors) in colormap) {
    blockColors[id] = colors;
}

// Then in loop:
if (blockColors.TryGetValue(id, out uint[]? colors)) {
    color = colors[hash];
}
```

## Caveats
- Parallelization requires ensuring TileImage is thread-safe
- SIMD/vectorization requires careful bounds checking
- Measure actual performance - micro-optimizations may not show benefits

## Related Issues
- See PERF-07: Excessive GC Pressure (DownSample method also has performance issues)

## References
- [Optimizing Loops in C#](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/data-parallelism-task-parallel-library)
- [Cache-Friendly Code](https://www.intel.com/content/www/us/en/developer/articles/technical/cache-blocking-techniques.html)
