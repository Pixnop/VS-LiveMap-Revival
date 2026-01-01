# HIGH-01: Race Condition - Shared State Access

## Metadata
- **Issue ID**: HIGH-01
- **Severity**: HIGH
- **Category**: Bugs / Concurrency
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/task/RenderTaskManager.cs`
- **Line**: 61
- **Method**: `Queue()`

## Description
The `Queue()` method checks if a region exists in either `_bufferQueue` or `_processQueue` before enqueueing, but these operations are not atomic. Between the `Contains()` check and the `Enqueue()` call, another thread could add the same region, resulting in duplicate entries.

## Impact
- **Duplicate Processing**: Same region rendered multiple times
- **Wasted CPU**: Unnecessary tile generation
- **I/O Overhead**: Redundant disk reads and writes
- **Increased Memory**: Duplicate BlockData allocations

## Risk Level
**HIGH** - This occurs during normal gameplay when chunks are modified. High-traffic servers will see frequent race conditions.

## Current Code

### Queue Method (Lines 52-69)
```csharp
public void Queue(int regionX, int regionZ) {
    if (_stopped) {
        return;
    }

    // convert region coordinates to long
    long index = Mathf.AsLong(regionX, regionZ);

    // ⚠️ Race condition: check and enqueue are not atomic
    // ensure this region hasn't already been queued up
    if (_bufferQueue.Contains(index) || _processQueue.Contains(index)) {
        return;
    }

    // queue it up to the buffer, so it doesn't get process immediately
    _bufferQueue.Enqueue(index);

    Logger.Debug($"Queueing region {regionX},{regionZ} (buffer: {_bufferQueue.Count} process:{_processQueue.Count})");
}
```

**Race Condition Scenario**:
1. Thread A: Checks `Contains()` - returns false
2. Thread B: Checks `Contains()` - returns false (before A enqueues)
3. Thread A: Calls `Enqueue()` - adds region
4. Thread B: Calls `Enqueue()` - adds same region again (duplicate!)

## Recommended Fix

### Option 1: Use ConcurrentHashSet (Recommended)
```csharp
// Replace ConcurrentQueue with HashSet for deduplication
private readonly HashSet<long> _queuedRegions = [];
private readonly object _queueLock = new();
private readonly ConcurrentQueue<long> _bufferQueue = new();
private readonly BlockingCollection<long> _processQueue = [];

public void Queue(int regionX, int regionZ) {
    if (_stopped) {
        return;
    }

    long index = Mathf.AsLong(regionX, regionZ);

    lock (_queueLock) {
        // ✅ Atomic check-and-add
        if (!_queuedRegions.Add(index)) {
            return; // Already queued
        }
    }

    _bufferQueue.Enqueue(index);
    Logger.Debug($"Queueing region {regionX},{regionZ} (buffer: {_bufferQueue.Count} process:{_processQueue.Count})");
}

public void ProcessQueue() {
    if (_stopped) {
        return;
    }

    if (_server.Colormap.Count == 0) {
        Logger.Warn("Cannot process render queue. No colormap loaded");
        return;
    }

    lock (_queueLock) {
        while (_bufferQueue.TryDequeue(out long region)) {
            _processQueue.Add(region);
        }
    }

    // ... rest of method
}

// In RenderTask completion or Dispose:
public void OnRegionComplete(long regionIndex) {
    lock (_queueLock) {
        _queuedRegions.Remove(regionIndex);
    }
}
```

### Option 2: Simple Lock (Simpler but slightly less performant)
```csharp
private readonly object _queueLock = new();

public void Queue(int regionX, int regionZ) {
    if (_stopped) {
        return;
    }

    long index = Mathf.AsLong(regionX, regionZ);

    lock (_queueLock) {
        // ✅ Atomic check and enqueue
        if (_bufferQueue.Contains(index) || _processQueue.Contains(index)) {
            return;
        }
        _bufferQueue.Enqueue(index);
    }

    Logger.Debug($"Queueing region {regionX},{regionZ} (buffer: {_bufferQueue.Count} process:{_processQueue.Count})");
}
```

## Benefits of Fix
1. **Thread Safety**: Eliminates race condition
2. **No Duplicates**: Guaranteed unique regions in queue
3. **Better Performance**: Avoids wasted rendering cycles
4. **Reduced I/O**: No redundant disk operations
5. **Clearer Intent**: HashSet explicitly models "set of queued regions"

## Testing Recommendations
1. Simulate rapid chunk updates from multiple threads
2. Add logging to detect duplicate region processing
3. Use thread sanitizer tools to verify no data races
4. Stress test with 10+ players modifying chunks simultaneously
5. Monitor queue sizes for unexpected growth

## Implementation Notes
- Option 1 requires tracking completion to remove from `_queuedRegions`
- Option 2 is simpler but locks more frequently
- Consider using `ConcurrentDictionary<long, byte>` as a concurrent set alternative
- The logging call should be outside the lock to minimize lock duration

## Related Issues
None

## References
- [Threading in C#: Part 2 - Concurrent Collections](https://www.albahari.com/threading/part5.aspx#_Concurrent_Collections)
- [Lock Statement (C# Reference)](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock)
