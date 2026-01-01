# PERF-06: Synchronous File I/O

## Metadata
- **Issue ID**: PERF-06
- **Priority**: MEDIUM IMPACT
- **Category**: Performance / I/O
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/data/Colormap.cs`
- **Lines**: 67, 82
- **Methods**: `LoadFromDisk()`, `SaveToDisk()`

## Description
The colormap loading and saving uses synchronous file I/O methods (`File.ReadAllText` and `File.WriteAllText`) which block threads. While these operations run on background threads, using async I/O would free up thread pool resources.

## Impact
- **Thread Pool Starvation**: Blocks thread pool threads during I/O
- **Reduced Throughput**: Fewer threads available for other work
- **Poor Scalability**: Issues compound under high load
- **Wasted CPU Time**: Thread sleeping instead of working

### Performance Impact Estimation
- **Current**: Thread blocked for duration of I/O (~10-50ms)
- **Optimized**: Thread freed immediately, I/O completes asynchronously
- **Improvement**: Better thread pool utilization, ~20-30% more efficient

## Risk Level
**MEDIUM IMPACT** - Affects server startup and colormap updates. Not critical but best practice to use async I/O.

## Current Code

### LoadFromDisk (Lines 63-79)
```csharp
public void LoadFromDisk(IWorldAccessor world) {
    new Thread(_ => {  // ⚠️ Thread instead of Task
        string? json = null;
        if (File.Exists(Files.ColormapFile)) {
            json = File.ReadAllText(Files.ColormapFile, Encoding.UTF8);  // ⚠️ Synchronous read
        }

        if (Deserialize(json)) {
            RefreshIds(world);
            Logger.Info("Colormap loaded from disk");
        }
        else {
            Logger.Warn("Could not load colormap from disk.");
            Logger.Warn("An admin needs to send the colormap from their client.");
        }
    }).Start();
}
```

### SaveToDisk (Lines 81-83)
```csharp
public void SaveToDisk() {
    File.WriteAllText(Files.ColormapFile, Serialize(), Encoding.UTF8);  // ⚠️ Synchronous write
}
```

## Recommended Fix

### Async Implementation
```csharp
public void LoadFromDisk(IWorldAccessor world) {
    // ✅ Use Task.Run instead of Thread
    Task.Run(async () => {
        try {
            string? json = null;
            if (File.Exists(Files.ColormapFile)) {
                // ✅ Async file read
                json = await File.ReadAllTextAsync(Files.ColormapFile, Encoding.UTF8);
            }

            if (Deserialize(json)) {
                RefreshIds(world);
                Logger.Info("Colormap loaded from disk");
            }
            else {
                Logger.Warn("Could not load colormap from disk.");
                Logger.Warn("An admin needs to send the colormap from their client.");
            }
        }
        catch (Exception e) {
            Logger.Error($"Error loading colormap from disk: {e}");
        }
    });
}

public async Task SaveToDiskAsync() {
    // ✅ Async file write
    await File.WriteAllTextAsync(Files.ColormapFile, Serialize(), Encoding.UTF8);
}

// Update LoadFromPacket to use async save
public void LoadFromPacket(IWorldAccessor world, ColormapPacket packet) {
    Task.Run(async () => {
        try {
            if (Deserialize(packet.Decompress().RawColormap)) {
                await SaveToDiskAsync();
                RefreshIds(world);
                Logger.Info("Colormap saved to disk");
            }
            else {
                Logger.Warn("Could not save colormap to disk");
            }
        }
        catch (Exception e) {
            Logger.Error($"Error processing colormap packet: {e}");
        }
    });
}
```

### Synchronous Wrapper (If Callers Need Sync API)
```csharp
// Keep synchronous version for backward compatibility
public void SaveToDisk() {
    SaveToDiskAsync().GetAwaiter().GetResult();
}

// But prefer async version
public async Task SaveToDiskAsync() {
    await File.WriteAllTextAsync(Files.ColormapFile, Serialize(), Encoding.UTF8);
}
```

## Benefits of Fix
1. **Better Thread Utilization**: Threads not blocked during I/O
2. **Improved Scalability**: Thread pool can handle more concurrent operations
3. **Modern Pattern**: Async/await is standard for I/O operations
4. **Exception Handling**: Better error propagation with async
5. **Cancellation Support**: Can add CancellationToken later

## Testing Recommendations
1. Load colormap on server startup and verify it works
2. Send colormap packets and verify saving works
3. Monitor thread pool statistics (via performance counters)
4. Test concurrent colormap operations
5. Verify error handling for corrupted files

## Implementation Notes
- **File.ReadAllTextAsync**: Available in .NET Core 2.0+
- **File.WriteAllTextAsync**: Available in .NET Core 2.0+
- **Task.Run**: Better than Thread for async work
- **ConfigureAwait**: Not needed here (no synchronization context)

## Refactoring Strategy

1. **Add async methods first**:
```csharp
public async Task LoadFromDiskAsync(IWorldAccessor world) { ... }
public async Task SaveToDiskAsync() { ... }
```

2. **Update internal calls to use async**:
```csharp
// In LoadFromPacket
await SaveToDiskAsync();
```

3. **Keep sync wrappers if needed**:
```csharp
public void SaveToDisk() => SaveToDiskAsync().GetAwaiter().GetResult();
```

4. **Eventually remove sync methods** when all callers updated

## Alternative Approaches

### Fire-and-Forget with Proper Tracking
```csharp
private Task? _saveTask;

public void SaveToDisk() {
    _saveTask = File.WriteAllTextAsync(Files.ColormapFile, Serialize(), Encoding.UTF8);
}

public void Dispose() {
    // ✅ Ensure save completes before disposal
    _saveTask?.GetAwaiter().GetResult();
    _colorsByName.Clear();
}
```

## Caveats
- Async is overkill for tiny files, but colormap can be 100KB+
- Ensure callers don't block on async methods (no `.Result` or `.Wait()`)
- Consider adding file write buffering if saves are very frequent

## Related Issues
- See CRITICAL-01: Thread Leak (same methods need fixing)
- Both issues can be fixed together in single refactoring

## References
- [Async File I/O](https://docs.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o)
- [Task-based Asynchronous Pattern](https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)
