# REFACTOR-01: Magic Numbers

## Metadata
- **Issue ID**: REFACTOR-01
- **Priority**: Code Quality
- **Category**: Refactoring / Readability
- **Status**: Open
- **Effort**: Easy

## Location
Multiple files contain hardcoded numeric literals:

1. **src/tile/TileImage.cs**: `512` appears 20+ times
2. **src/render/BasicRenderer.cs**: `512` used in loops
3. **src/task/RenderTask.cs**: `32`, `16`, `5` scattered throughout
4. **src/data/BlockData.cs**: `512` in array size and indexing
5. **web/src/LiveMap.ts**: Various numeric constants

## Description
Numeric literals ("magic numbers") appear throughout the code without explanation. Values like `512`, `32`, `16` represent important domain concepts (region size, chunk size, chunks per region) but aren't named, making the code harder to understand and maintain.

## Impact
- **Reduced Readability**: Unclear what numbers represent
- **Maintenance Burden**: Hard to change values consistently
- **Error Prone**: Easy to use wrong value
- **Poor Documentation**: No explanation of significance

## Examples

### TileImage.cs
```csharp
public TileImage(int regionX, int regionZ) {
    _bitmap = new SKBitmap(512, 512);  // ⚠️ What is 512?
    _shadowMap = new byte[512 << 9].Fill((byte)128);  // ⚠️ Magic bit shift
    // ...
}

public void SetBlockColor(int blockX, int blockZ, uint argb, float yDiff) {
    int imgX = blockX & 511;  // ⚠️ Why 511?
    int imgZ = blockZ & 511;
    // ...
}
```

### BasicRenderer.cs
```csharp
for (int x = 0; x < 512; x++) {  // ⚠️ Hardcoded limit
    for (int z = 0; z < 512; z++) {
        // ...
    }
}
```

### RenderTask.cs
```csharp
int chunkX1 = regionX << 4;  // ⚠️ What is 4?
int chunkX2 = chunkX1 + 16;   // ⚠️ What is 16?
```

## Recommended Fix

### Define Constants Class
```csharp
// Create new file: src/constants/WorldConstants.cs
namespace livemap.constants;

public static class WorldConstants {
    // Region dimensions (in blocks)
    public const int REGION_SIZE_BLOCKS = 512;
    public const int REGION_SIZE_MASK = REGION_SIZE_BLOCKS - 1;  // 511

    // Chunk dimensions
    public const int CHUNK_SIZE_BLOCKS = 32;
    public const int CHUNKS_PER_REGION = 16;  // 512 / 32
    public const int CHUNKS_PER_REGION_SHIFT = 4;  // log2(16)

    // Bit shifts for coordinate conversion
    public const int CHUNK_TO_REGION_SHIFT = 4;  // 2^4 = 16 chunks per region
    public const int BLOCK_TO_CHUNK_SHIFT = 5;   // 2^5 = 32 blocks per chunk

    // Other constants
    public const int DEFAULT_SHADOW_VALUE = 128;
}
```

### Updated TileImage.cs
```csharp
using livemap.constants;

public TileImage(int regionX, int regionZ) {
    // ✅ Clear intent
    _bitmap = new SKBitmap(WorldConstants.REGION_SIZE_BLOCKS, WorldConstants.REGION_SIZE_BLOCKS);
    _shadowMap = new byte[WorldConstants.REGION_SIZE_BLOCKS * WorldConstants.REGION_SIZE_BLOCKS]
        .Fill((byte)WorldConstants.DEFAULT_SHADOW_VALUE);

    _bitmapRowBytes = _bitmap.RowBytes;
    _regionX = regionX;
    _regionZ = regionZ;
}

public void SetBlockColor(int blockX, int blockZ, uint argb, float yDiff) {
    // ✅ Clear masking operation
    int imgX = blockX & WorldConstants.REGION_SIZE_MASK;
    int imgZ = blockZ & WorldConstants.REGION_SIZE_MASK;

    ((uint*)(_bitmapPtr + imgZ * _bitmapRowBytes))[imgX] = argb;

    int index = (imgZ << WorldConstants.BLOCK_TO_CHUNK_SHIFT) + imgX;
    _shadowMap[index] = (byte)(_shadowMap[index] * yDiff);
}
```

### Updated RenderTask.cs
```csharp
using livemap.constants;

public void ScanRegion(int regionX, int regionZ) {
    // ✅ Self-documenting code
    int chunkX1 = regionX << WorldConstants.CHUNK_TO_REGION_SHIFT;
    int chunkZ1 = regionZ << WorldConstants.CHUNK_TO_REGION_SHIFT;
    int chunkX2 = chunkX1 + WorldConstants.CHUNKS_PER_REGION;
    int chunkZ2 = chunkZ1 + WorldConstants.CHUNKS_PER_REGION;

    // ...
}
```

### Updated BasicRenderer.cs
```csharp
using livemap.constants;

public override void ProcessBlockData(int regionX, int regionZ, BlockData blockData) {
    if (TileImage == null) {
        return;
    }

    // ✅ Clear loop bounds
    for (int x = 0; x < WorldConstants.REGION_SIZE_BLOCKS; x++) {
        for (int z = 0; z < WorldConstants.REGION_SIZE_BLOCKS; z++) {
            // ...
        }
    }
}
```

## Benefits of Fix
1. **Self-Documenting**: Constants explain their purpose
2. **Maintainability**: Change value in one place
3. **Type Safety**: Compiler catches misuse
4. **Searchability**: Easy to find all uses of a concept
5. **Documentation**: Constants serve as documentation

## Testing Recommendations
1. Verify all magic numbers are replaced
2. Run full test suite to ensure no regressions
3. Compile and verify no warnings
4. Grep for remaining hardcoded values
5. Validate tile rendering still works correctly

## Implementation Strategy

1. **Create constants file** with all domain constants
2. **Add XML documentation** to each constant
3. **Replace magic numbers** file by file
4. **Update tests** to use constants
5. **Add code analysis rule** to prevent future magic numbers

## Complete Constants File

```csharp
namespace livemap.constants;

/// <summary>
/// World and rendering constants for LiveMap.
/// </summary>
public static class WorldConstants {
    /// <summary>Region size in blocks (512x512).</summary>
    public const int REGION_SIZE_BLOCKS = 512;

    /// <summary>Bitmask for region coordinates (511).</summary>
    public const int REGION_SIZE_MASK = REGION_SIZE_BLOCKS - 1;

    /// <summary>Chunk size in blocks (32x32).</summary>
    public const int CHUNK_SIZE_BLOCKS = 32;

    /// <summary>Number of chunks per region dimension (16).</summary>
    public const int CHUNKS_PER_REGION = REGION_SIZE_BLOCKS / CHUNK_SIZE_BLOCKS;

    /// <summary>Bit shift for chunk to region conversion (4 = log2(16)).</summary>
    public const int CHUNK_TO_REGION_SHIFT = 4;

    /// <summary>Bit shift for block to chunk conversion (5 = log2(32)).</summary>
    public const int BLOCK_TO_CHUNK_SHIFT = 5;

    /// <summary>Default shadow map value (128 = neutral).</summary>
    public const byte DEFAULT_SHADOW_VALUE = 128;

    /// <summary>Maximum zoom level for tile generation.</summary>
    public const int MAX_ZOOM_LEVELS = 8;
}
```

## Caveats
- Don't go overboard: `0`, `1`, `-1` usually don't need constants
- Use meaningful names: `MAX_PLAYERS` not `CONST_42`
- Group related constants together

## Related Issues
None

## References
- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [CA1802: Use literals where appropriate](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1802)
