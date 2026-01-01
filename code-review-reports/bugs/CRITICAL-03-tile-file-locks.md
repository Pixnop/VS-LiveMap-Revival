# CRITICAL-03: Potential File Lock Issues in TileImage.Save()

## Metadata
- **Issue ID**: CRITICAL-03
- **Severity**: CRITICAL
- **Category**: Bugs / File I/O
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/tile/TileImage.cs`
- **Lines**: 64, 69, 74
- **Method**: `Save()`

## Description
The `Save()` method opens the same file multiple times without properly disposing streams between operations. Lines 64 and 69 both open `FileStream` on the same `fileInfo`, and the first stream may not be disposed before the second opens, leading to potential file locks and corruption.

## Impact
- **File Access Exceptions**: `IOException` due to file being locked
- **Corrupted Tile Images**: Incomplete writes if streams interfere
- **Failed Tile Updates**: Players see outdated map tiles
- **System Instability**: Accumulation of locked files

## Risk Level
**CRITICAL** - This affects core map rendering functionality. Failed tile saves mean the map won't update correctly for players.

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
                // ⚠️ Opens file for reading
                using FileStream inStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                SKBitmap bitmap = SKBitmap.Decode(inStream) ?? new SKBitmap(512, 512);

                WritePixels(bitmap, zoom);

                // ⚠️ Opens same file again while inStream may still be open
                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
                bitmap.Dispose();
            }
            else {
                using FileStream outStream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                _bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
            }
        }

        _bitmap.Dispose();
    }
    catch (Exception e) {
        Logger.Error(e.ToString());
    }
}
```

## Recommended Fix

```csharp
public void Save(string rendererId) {
    try {
        Config config = LiveMap.Api.Config;
        for (int zoom = 0; zoom <= config.Zoom.MaxOut; zoom++) {
            FileInfo fileInfo = new(Path.Combine(Files.TilesDir, rendererId, zoom.ToString(), $"{_regionX >> zoom}_{_regionZ >> zoom}.{config.Web.TileType.Type}"));
            GamePaths.EnsurePathExists(fileInfo.Directory!.FullName);

            if (zoom > 0) {
                SKBitmap bitmap;

                // ✅ Read existing tile if it exists
                if (fileInfo.Exists) {
                    using (FileStream inStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        bitmap = SKBitmap.Decode(inStream) ?? new SKBitmap(512, 512);
                    } // ✅ Stream disposed here before next open
                } else {
                    bitmap = new SKBitmap(512, 512);
                }

                WritePixels(bitmap, zoom);

                // ✅ Write to file (stream from read is already disposed)
                using (FileStream outStream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.None)) {
                    bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
                    outStream.Flush(); // ✅ Ensure data is written
                }

                bitmap.Dispose();
            }
            else {
                using FileStream outStream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.None);
                _bitmap.Encode(config.Web.TileType.Format, config.Web.TileQuality).SaveTo(outStream);
                outStream.Flush();
            }
        }

        _bitmap.Dispose();
    }
    catch (Exception e) {
        Logger.Error(e.ToString());
    }
}
```

## Benefits of Fix
1. **No File Locks**: Streams properly disposed before reopening
2. **Correct FileMode**: Use `FileMode.Create` for writing to truncate existing content
3. **Explicit Scoping**: Clear separation between read and write operations
4. **Better Performance**: `FileMode.Create` is more efficient than `OpenOrCreate` for overwrites
5. **Reduced Risk**: `FileShare.None` prevents concurrent access during write

## Testing Recommendations
1. Generate tiles rapidly and monitor for `IOException`
2. Verify tiles update correctly during concurrent rendering
3. Check for orphaned lock files in tile directory
4. Test with antivirus software active (common cause of file lock issues)
5. Stress test with multiple regions rendering simultaneously

## Implementation Notes
- The nested `using` blocks ensure proper disposal order
- `FileMode.Create` truncates existing files, which is correct behavior for tile updates
- `FileShare.None` during write prevents partial reads by web server
- Consider adding retry logic for transient file lock failures

## Related Issues
- See HIGH-02: Thread Safety - Bitmap Access (related to bitmap disposal timing)

## References
- [Microsoft Docs: FileStream Class](https://docs.microsoft.com/en-us/dotnet/api/system.io.filestream)
- [FileMode Enumeration](https://docs.microsoft.com/en-us/dotnet/api/system.io.filemode)
