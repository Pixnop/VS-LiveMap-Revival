# HIGH-02: Thread Safety - Bitmap Access

## Metadata
- **Issue ID**: HIGH-02
- **Severity**: HIGH
- **Category**: Bugs / Thread Safety
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/tile/TileImage.cs`
- **Line**: 79
- **Method**: `Save()`

## Description
The `_bitmap.Dispose()` is called at line 79 in the `Save()` method, but `WritePixels()` (called at line 67) accesses `_bitmap` through the `_bitmapPtr` pointer. If multiple zoom levels are processed and the bitmap is disposed while still being accessed, this causes `ObjectDisposedException` or memory corruption.

## Impact
- **ObjectDisposedException**: Accessing disposed bitmap
- **Access Violations**: Dereferencing freed memory via pointer
- **Corrupted Tiles**: Partial or incorrect pixel data
- **Crashes**: Potential segmentation faults from unsafe pointer access

## Risk Level
**HIGH** - The unsafe code using pointers makes this particularly dangerous. Memory corruption can cause hard-to-diagnose crashes.

## Current Code

### Save Method (Lines 56-84)
```csharp
public void Save(string rendererId) {
    try {
        Config config = LiveMap.Api.Config;
        for (int zoom = 0; zoom <= config.Zoom.MaxOut; zoom++) {
            FileInfo fileInfo = new(Path.Combine(Files.TilesDir, rendererId, zoom.ToString(), $"{_regionX >> zoom}_{_regionZ >> zoom}.{config.Web.TileType.Type}"));
            GamePaths.EnsurePathExists(fileInfo.Directory!.FullName);

            if (zoom > 0) {
                using FileStream inStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                SKBitmap bitmap = SKBitmap.Decode(inStream) ?? new SKBitmap(512, 512);

                WritePixels(bitmap, zoom);  // ⚠️ Accesses _bitmap via _bitmapPtr

                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
                bitmap.Dispose();
            }
            else {
                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                _bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
            }
        }

        _bitmap.Dispose();  // ⚠️ Disposed here, but may still be accessed
    }
    catch (Exception e) {
        Logger.Error(e.ToString());
    }
}
```

### WritePixels Method (Lines 86-109)
```csharp
private void WritePixels(SKBitmap png, int zoom) {
    int step = 1 << zoom;
    int baseX = ((_regionX * 512) >> zoom) & 511;
    int baseZ = ((_regionZ * 512) >> zoom) & 511;
    byte* pngPtr = (byte*)png.GetPixels().ToPointer();
    int pngRowBytes = png.RowBytes;
    for (int x = 0; x < 512; x += step) {
        for (int z = 0; z < 512; z += step) {
            uint argb = ((uint*)(_bitmapPtr + z * _bitmapRowBytes))[x];  // ⚠️ Uses _bitmapPtr
            // ...
        }
    }
}
```

## Recommended Fix

### Option 1: Dispose Only After Loop Completes (Recommended)
```csharp
public void Save(string rendererId) {
    try {
        Config config = LiveMap.Api.Config;
        for (int zoom = 0; zoom <= config.Zoom.MaxOut; zoom++) {
            FileInfo fileInfo = new(Path.Combine(Files.TilesDir, rendererId, zoom.ToString(), $"{_regionX >> zoom}_{_regionZ >> zoom}.{config.Web.TileType.Type}"));
            GamePaths.EnsurePathExists(fileInfo.Directory!.FullName);

            if (zoom > 0) {
                using FileStream inStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                SKBitmap bitmap = SKBitmap.Decode(inStream) ?? new SKBitmap(512, 512);

                WritePixels(bitmap, zoom);  // ✅ _bitmap still valid

                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
                bitmap.Dispose();
            }
            else {
                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                _bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
            }
        }
        // ✅ Dispose only after all zoom levels processed
    }
    catch (Exception e) {
        Logger.Error(e.ToString());
    }
    finally {
        // ✅ Use finally to ensure disposal even on exception
        _bitmap.Dispose();
    }
}
```

### Option 2: Make TileImage IDisposable (Better Design)
```csharp
public unsafe class TileImage : IDisposable {
    private SKBitmap? _bitmap;
    private byte* _bitmapPtr;
    private bool _disposed;

    public void Save(string rendererId) {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(TileImage));
        }

        try {
            // ... existing save logic without final _bitmap.Dispose()
        }
        catch (Exception e) {
            Logger.Error(e.ToString());
        }
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _bitmap?.Dispose();
        _bitmap = null;
        _disposed = true;
    }
}

// Caller code:
using TileImage tileImage = new TileImage(regionX, regionZ);
// ... use tileImage ...
tileImage.Save(rendererId);
// Automatically disposed here
```

## Benefits of Fix
1. **Memory Safety**: No access to disposed objects
2. **Correct Disposal**: Bitmap disposed only after all uses complete
3. **Exception Safety**: `finally` block ensures cleanup
4. **Clear Ownership**: IDisposable pattern makes ownership explicit

## Testing Recommendations
1. Generate tiles with multiple zoom levels and verify no crashes
2. Run under memory profiler to detect use-after-free
3. Enable .NET runtime checks for disposed object access
4. Stress test with rapid tile generation
5. Test with high zoom level configurations (MaxOut > 5)

## Implementation Notes
- The `unsafe` keyword makes this code particularly sensitive to disposal timing
- Consider removing `unsafe` code if performance isn't critical
- The pointer `_bitmapPtr` becomes invalid after `_bitmap.Dispose()`
- Option 2 provides better encapsulation and follows RAII principles

## Related Issues
- See CRITICAL-03: File Lock Issues (also in Save method)

## References
- [Unsafe Code and Pointers (C# Programming Guide)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/unsafe-code-pointers/)
- [IDisposable Interface](https://docs.microsoft.com/en-us/dotnet/api/system.idisposable)
