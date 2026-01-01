# PERF-07: Excessive GC Pressure in DownSample

## Metadata
- **Issue ID**: PERF-07
- **Priority**: MEDIUM IMPACT
- **Category**: Performance / Memory
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/tile/TileImage.cs`
- **Lines**: 111-128
- **Method**: `DownSample()`

## Description
The `DownSample` method is called in a nested loop for every non-zero tile at zoom levels > 0. The method creates temporary variables in nested loops, causing repeated allocations. For a typical 512x512 tile at zoom level 3, this method is called ~64K times.

## Impact
- **Excessive Allocations**: Temporary variables created in hot loop
- **GC Pressure**: Frequent Gen0 collections
- **Cache Misses**: Poor memory locality
- **Slower Rendering**: GC pauses during tile generation

### Performance Impact Estimation
- **Current**: ~500-1000 Gen0 collections per 100 tiles
- **Optimized**: ~100-200 Gen0 collections per 100 tiles
- **Improvement**: ~50-80% reduction in GC pressure

## Risk Level
**MEDIUM IMPACT** - Called very frequently during tile rendering. GC pauses affect user experience.

## Current Code

### DownSample Method (Lines 111-128)
```csharp
private uint DownSample(int x, int z, uint argb, int step) {
    uint a = 0, r = 0, g = 0, b = 0, c = 0;  // ⚠️ Created on every call
    for (int i = 0; i < step; i++) {
        for (int j = 0; j < step; j++) {
            if (i != 0 && j != 0) {
                argb = ((uint*)(_bitmapPtr + (z + j) * _bitmapRowBytes))[x + i];
            }

            a += argb >> 24 & 0xFF;
            r += argb >> 16 & 0xFF;
            g += argb >> 8 & 0xFF;
            b += argb >> 0 & 0xFF;
            c++;
        }
    }

    return c == 0 ? 0 : (a / c) << 24 | (r / c) << 16 | (g / c) << 8 | (b / c);
}
```

### Usage in WritePixels (Lines 86-109)
```csharp
private void WritePixels(SKBitmap png, int zoom) {
    int step = 1 << zoom;
    int baseX = ((_regionX * 512) >> zoom) & 511;
    int baseZ = ((_regionZ * 512) >> zoom) & 511;
    byte* pngPtr = (byte*)png.GetPixels().ToPointer();
    int pngRowBytes = png.RowBytes;

    for (int x = 0; x < 512; x += step) {
        for (int z = 0; z < 512; z += step) {
            uint argb = ((uint*)(_bitmapPtr + z * _bitmapRowBytes))[x];
            if (argb == 0) {
                continue;
            }

            if (step > 1) {
                argb = DownSample(x, z, argb, step);  // ⚠️ Called ~64K times for zoom 3
            }

            ((uint*)(pngPtr + (baseZ + (z >> zoom)) * pngRowBytes))[baseX + (x >> zoom)] = argb;
        }
    }
}
```

## Recommended Fix

### Option 1: Inline DownSample Logic
```csharp
private void WritePixels(SKBitmap png, int zoom) {
    int step = 1 << zoom;
    int baseX = ((_regionX * 512) >> zoom) & 511;
    int baseZ = ((_regionZ * 512) >> zoom) & 511;
    byte* pngPtr = (byte*)png.GetPixels().ToPointer();
    int pngRowBytes = png.RowBytes;

    for (int x = 0; x < 512; x += step) {
        for (int z = 0; z < 512; z += step) {
            uint argb = ((uint*)(_bitmapPtr + z * _bitmapRowBytes))[x];
            if (argb == 0) {
                continue;
            }

            if (step > 1) {
                // ✅ Inline downsampling to avoid method call overhead
                uint a = 0, r = 0, g = 0, b = 0, c = 0;
                for (int i = 0; i < step; i++) {
                    for (int j = 0; j < step; j++) {
                        if (i != 0 || j != 0) {
                            argb = ((uint*)(_bitmapPtr + (z + j) * _bitmapRowBytes))[x + i];
                        }
                        a += (argb >> 24) & 0xFF;
                        r += (argb >> 16) & 0xFF;
                        g += (argb >> 8) & 0xFF;
                        b += argb & 0xFF;
                        c++;
                    }
                }
                argb = c == 0 ? 0 : (a / c) << 24 | (r / c) << 16 | (g / c) << 8 | (b / c);
            }

            ((uint*)(pngPtr + (baseZ + (z >> zoom)) * pngRowBytes))[baseX + (x >> zoom)] = argb;
        }
    }
}
```

### Option 2: Use Stackalloc for Temporary Buffers
```csharp
private unsafe uint DownSample(int x, int z, uint argb, int step) {
    // ✅ Stack allocation for small arrays (zero GC pressure)
    Span<uint> components = stackalloc uint[5]; // a, r, g, b, c

    for (int i = 0; i < step; i++) {
        for (int j = 0; j < step; j++) {
            if (i != 0 || j != 0) {
                argb = ((uint*)(_bitmapPtr + (z + j) * _bitmapRowBytes))[x + i];
            }

            components[0] += (argb >> 24) & 0xFF;  // a
            components[1] += (argb >> 16) & 0xFF;  // r
            components[2] += (argb >> 8) & 0xFF;   // g
            components[3] += argb & 0xFF;           // b
            components[4]++;                         // c
        }
    }

    uint c = components[4];
    return c == 0 ? 0 :
        (components[0] / c) << 24 |
        (components[1] / c) << 16 |
        (components[2] / c) << 8 |
        (components[3] / c);
}
```

### Option 3: Add AggressiveInlining
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private uint DownSample(int x, int z, uint argb, int step) {
    // ✅ Compiler will inline this method, eliminating call overhead
    uint a = 0, r = 0, g = 0, b = 0, c = 0;

    for (int i = 0; i < step; i++) {
        for (int j = 0; j < step; j++) {
            if (i != 0 || j != 0) {
                argb = ((uint*)(_bitmapPtr + (z + j) * _bitmapRowBytes))[x + i];
            }

            a += (argb >> 24) & 0xFF;
            r += (argb >> 16) & 0xFF;
            g += (argb >> 8) & 0xFF;
            b += argb & 0xFF;
            c++;
        }
    }

    return c == 0 ? 0 : (a / c) << 24 | (r / c) << 16 | (g / c) << 8 | (b / c);
}
```

## Benefits of Fix
1. **Reduced GC Pressure**: Fewer allocations = fewer collections
2. **Better Performance**: Inlining eliminates method call overhead
3. **Improved Cache Locality**: Less pointer chasing
4. **Faster Rendering**: Fewer GC pauses during tile generation

## Testing Recommendations
1. Profile GC collections before/after with dotMemory
2. Benchmark tile rendering for 100 regions
3. Verify downsampled tiles look identical
4. Test with various zoom levels (especially high zoom)
5. Monitor Gen0 collection frequency

## Implementation Notes
- **Inlining**: Modern JIT compiler may already inline this, but `AggressiveInlining` guarantees it
- **Stackalloc**: Only use for small, fixed-size allocations
- **Unsafe Code**: Already using `unsafe`, so stackalloc is appropriate
- **Primitives**: uint variables are value types (stack allocated), not heap allocated
  - *Actually, the issue is less about allocation and more about method call overhead*

## Alternative Approaches

### Parallel Processing Per Tile
```csharp
private void WritePixels(SKBitmap png, int zoom) {
    int step = 1 << zoom;
    // ...

    // ✅ Process in parallel (if thread-safe)
    Parallel.For(0, 512 / step, xBlock => {
        int x = xBlock * step;
        for (int z = 0; z < 512; z += step) {
            // ... downsampling logic ...
        }
    });
}
```

## Caveats
- Stackalloc can cause stack overflow if overused
- Inlining may increase code size (rarely an issue)
- Parallel processing requires thread-safe bitmap access

## Related Issues
- See PERF-02: Block Iteration (similar hot loop optimization)
- See HIGH-02: Bitmap Thread Safety (affects parallel processing)

## References
- [Span and Memory](https://docs.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
- [MethodImplOptions.AggressiveInlining](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.methodimploptions)
- [Stackalloc](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/stackalloc)
