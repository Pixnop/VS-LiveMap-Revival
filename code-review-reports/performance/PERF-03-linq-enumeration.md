# PERF-03: Redundant LINQ in GetAllMapPositions

## Metadata
- **Issue ID**: PERF-03
- **Priority**: HIGH IMPACT
- **Category**: Performance / Database
- **Status**: Open
- **Effort**: Easy

## Location
- **File**: `src/data/ChunkLoader.cs`
- **Lines**: 43-50
- **Methods**: `GetAllMapPositions()`, `GetAllMapRegionPositions()`, `GetAllMapChunkPositions()`

## Description
The method returns an `IEnumerable<ChunkPos>` with deferred execution. If the result is enumerated multiple times (common in LINQ chains), it executes the database query each time. This is a classic N+1 query problem disguised as deferred execution.

## Impact
- **Multiple Database Queries**: Same query executed repeatedly
- **Slow Rendering**: Each enumeration hits the database
- **Resource Waste**: Unnecessary SQL executions
- **Lock Contention**: Multiple concurrent reads

### Performance Impact Estimation
- **Current**: Query executed N times if enumerated N times
- **Optimized**: Query executed once, cached in memory
- **Improvement**: Up to **10x faster** for code that enumerates twice

## Risk Level
**HIGH IMPACT** - Used in `RenderTask.ScanRegion()` which filters the results. If the filtering code enumerates multiple times, this causes severe performance issues.

## Current Code

### GetAllMapPositions (Lines 43-50)
```csharp
private IEnumerable<ChunkPos> GetAllMapPositions(string type) {
    using SqliteCommand sqlite = _sqliteConn.CreateCommand();
    sqlite.CommandText = $"SELECT position FROM map{type}";
    using SqliteDataReader reader = sqlite.ExecuteReader();
    while (reader.Read()) {
        yield return ChunkPos.FromChunkIndex_saveGamev2((ulong)(long)reader["position"]);
    }
    // ⚠️ Returns IEnumerable with deferred execution
}
```

### Usage in RenderTask.cs (Lines 33-34)
```csharp
IEnumerable<ChunkPos> chunks = renderTaskManager.ChunkLoader.GetAllMapChunkPositions()
    .Where(chunkPos => chunkPos.X >= chunkX1 && chunkPos.Z >= chunkZ1 && chunkPos.X < chunkX2 && chunkPos.Z < chunkZ2);
// ⚠️ If chunks is enumerated multiple times, query runs multiple times!
```

## Recommended Fix

### Option 1: Materialize to List (Simple)
```csharp
private IEnumerable<ChunkPos> GetAllMapPositions(string type) {
    using SqliteCommand sqlite = _sqliteConn.CreateCommand();
    sqlite.CommandText = $"SELECT position FROM map{type}";
    using SqliteDataReader reader = sqlite.ExecuteReader();

    // ✅ Materialize to list immediately
    var positions = new List<ChunkPos>();
    while (reader.Read()) {
        positions.Add(ChunkPos.FromChunkIndex_saveGamev2((ulong)(long)reader["position"]));
    }

    return positions;
}
```

### Option 2: Cache Results (If Data Doesn't Change Often)
```csharp
private readonly ConcurrentDictionary<string, List<ChunkPos>> _positionCache = new();
private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
private DateTime _lastCacheRefresh = DateTime.MinValue;

private IEnumerable<ChunkPos> GetAllMapPositions(string type) {
    // ✅ Use cached results if fresh
    if (DateTime.UtcNow - _lastCacheRefresh < _cacheExpiration) {
        if (_positionCache.TryGetValue(type, out var cached)) {
            return cached;
        }
    }

    using SqliteCommand sqlite = _sqliteConn.CreateCommand();
    sqlite.CommandText = $"SELECT position FROM map{type}";
    using SqliteDataReader reader = sqlite.ExecuteReader();

    var positions = new List<ChunkPos>();
    while (reader.Read()) {
        positions.Add(ChunkPos.FromChunkIndex_saveGamev2((ulong)(long)reader["position"]));
    }

    _positionCache[type] = positions;
    _lastCacheRefresh = DateTime.UtcNow;

    return positions;
}
```

### Option 3: Filter in SQL (Best Performance)
```csharp
// Add new method that filters in database
public IEnumerable<ChunkPos> GetMapChunkPositionsInRegion(int chunkX1, int chunkZ1, int chunkX2, int chunkZ2) {
    using SqliteCommand sqlite = _sqliteConn.CreateCommand();

    // ✅ Filter in SQL instead of in memory
    sqlite.CommandText = @"
        SELECT position
        FROM mapchunk
        WHERE
            (position >> 32) >= @x1 AND
            (position >> 32) < @x2 AND
            (position & 0xFFFFFFFF) >= @z1 AND
            (position & 0xFFFFFFFF) < @z2
    ";

    sqlite.Parameters.AddWithValue("@x1", chunkX1);
    sqlite.Parameters.AddWithValue("@x2", chunkX2);
    sqlite.Parameters.AddWithValue("@z1", chunkZ1);
    sqlite.Parameters.AddWithValue("@z2", chunkZ2);

    using SqliteDataReader reader = sqlite.ExecuteReader();

    var positions = new List<ChunkPos>();
    while (reader.Read()) {
        positions.Add(ChunkPos.FromChunkIndex_saveGamev2((ulong)(long)reader["position"]));
    }

    return positions;
}
```

## Benefits of Fix
1. **Single Query**: Database accessed once instead of N times
2. **Faster Filtering**: In-memory filtering is cheap
3. **Predictable Performance**: No hidden re-execution
4. **Option 3**: Filtering in SQL is orders of magnitude faster

## Testing Recommendations
1. Add logging to track query execution count
2. Benchmark rendering 10 regions before/after
3. Profile database query frequency
4. Verify filtered results are identical
5. Test with large maps (10,000+ chunks)

## Implementation Notes
- **Memory Trade-off**: Materializing uses more memory but far less CPU
- **Cache Invalidation**: If using Option 2, may need to invalidate on chunk generation
- **SQL Filtering**: Option 3 is ideal if chunk index format is understood
- **Database Size**: Typical map has 1000-10000 chunks, so memory impact is minimal (~100KB-1MB)

## Usage Analysis

Need to verify how results are used:
```csharp
// In RenderTask.cs - check if enumerated multiple times
foreach (ChunkPos chunkPos in chunks) {  // First enumeration
    ScanChunkColumn(region, chunkPos, blockData);
}
// If chunks is used again here, it would re-query! ⚠️
```

## Related Issues
- See CRITICAL-02: SqliteCommand Not Disposed (same file)

## References
- [Deferred Execution in LINQ](https://docs.microsoft.com/en-us/dotnet/standard/linq/deferred-execution-example)
- [Multiple Enumeration Pitfalls](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1851)
