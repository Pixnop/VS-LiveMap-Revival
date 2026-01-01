# CRITICAL-01: Resource Leak - Unreleased Threads in Colormap

## Metadata
- **Issue ID**: CRITICAL-01
- **Severity**: CRITICAL
- **Category**: Bugs / Resource Management
- **Status**: Open
- **Effort**: Easy

## Location
- **File**: `src/data/Colormap.cs`
- **Lines**: 51, 64
- **Methods**: `LoadFromPacket()`, `LoadFromDisk()`

## Description
Threads are created and started without being tracked, joined, or properly managed. The code uses the old `new Thread(...).Start()` pattern which creates unmanaged threads that are never cleaned up.

## Impact
- **Memory Leaks**: Thread objects accumulate on repeated config reloads
- **Resource Exhaustion**: Eventually could exhaust thread pool resources
- **Unpredictable Behavior**: Orphaned threads may continue running after restart/reload
- **Difficult Debugging**: No visibility into thread lifecycle

## Risk Level
**CRITICAL** - This issue will cause resource leaks every time the configuration is reloaded or a colormap packet is received.

## Current Code

### LoadFromPacket (Line 50-61)
```csharp
public void LoadFromPacket(IWorldAccessor world, ColormapPacket packet) {
    new Thread(_ => {
        if (Deserialize(packet.Decompress().RawColormap)) {
            SaveToDisk();
            RefreshIds(world);
            Logger.Info("Colormap saved to disk");
        }
        else {
            Logger.Warn("Could not save colormap to disk");
        }
    }).Start();  // ⚠️ Thread never tracked or joined
}
```

### LoadFromDisk (Line 63-79)
```csharp
public void LoadFromDisk(IWorldAccessor world) {
    new Thread(_ => {
        string? json = null;
        if (File.Exists(Files.ColormapFile)) {
            json = File.ReadAllText(Files.ColormapFile, Encoding.UTF8);
        }

        if (Deserialize(json)) {
            RefreshIds(world);
            Logger.Info("Colormap loaded from disk");
        }
        else {
            Logger.Warn("Could not load colormap from disk.");
            Logger.Warn("An admin needs to send the colormap from their client.");
        }
    }).Start();  // ⚠️ Thread never tracked or joined
}
```

## Recommended Fix

### Option 1: Use Task.Run (Recommended)
```csharp
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
            Logger.Error($"Error loading colormap from packet: {e}");
        }
    });
}

public void LoadFromDisk(IWorldAccessor world) {
    Task.Run(async () => {
        try {
            string? json = null;
            if (File.Exists(Files.ColormapFile)) {
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

// Also update SaveToDisk to be async
public async Task SaveToDiskAsync() {
    await File.WriteAllTextAsync(Files.ColormapFile, Serialize(), Encoding.UTF8);
}
```

### Option 2: Track and Join Threads (Not Recommended)
```csharp
private Thread? _loadThread;
private readonly object _threadLock = new();

public void LoadFromPacket(IWorldAccessor world, ColormapPacket packet) {
    lock (_threadLock) {
        _loadThread?.Join(TimeSpan.FromSeconds(5));

        _loadThread = new Thread(_ => {
            // ... existing code ...
        });
        _loadThread.IsBackground = true;
        _loadThread.Start();
    }
}

public void Dispose() {
    lock (_threadLock) {
        _loadThread?.Join(TimeSpan.FromSeconds(5));
    }
    _colorsByName.Clear();
}
```

## Benefits of Fix
1. **Automatic Cleanup**: Task-based approach uses thread pool, eliminating leaks
2. **Better Error Handling**: Tasks support exception propagation
3. **Async/Await**: Modern pattern, easier to maintain
4. **Cancellation Support**: Can add CancellationToken for graceful shutdown
5. **Improved Performance**: Thread pool reuse vs creating new threads

## Testing Recommendations
1. Reload configuration 100 times and monitor thread count using Process Explorer
2. Send colormap packets repeatedly and check for leaks
3. Use dotMemory or PerfView to verify thread cleanup
4. Test behavior during mod unload/reload cycles
5. Monitor memory usage over extended gameplay sessions

## Related Issues
- See PERF-06: Synchronous File I/O - also addresses async file operations
- Consider adding CancellationToken support for graceful shutdown

## References
- [Microsoft Docs: Task-based Asynchronous Pattern](https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)
- [Why Thread.Start() is discouraged](https://blog.stephencleary.com/2013/08/startnew-is-dangerous.html)
