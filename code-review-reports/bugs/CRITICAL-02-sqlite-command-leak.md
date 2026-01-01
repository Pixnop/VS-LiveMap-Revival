# CRITICAL-02: Resource Leak - SqliteCommand Not Disposed

## Metadata
- **Issue ID**: CRITICAL-02
- **Severity**: CRITICAL
- **Category**: Bugs / Resource Management
- **Status**: Open
- **Effort**: Trivial

## Location
- **File**: `src/data/ChunkLoader.cs`
- **Line**: 70
- **Method**: `GetTableData()`

## Description
A `SqliteCommand` object is created but not wrapped in a `using` statement, meaning it will not be disposed properly. This leads to resource leaks as the command objects accumulate without being released.

## Impact
- **Connection Pool Exhaustion**: Undisposed commands can hold database resources
- **Database Locks**: May cause lock contention issues
- **Memory Leaks**: Command objects accumulate in memory
- **Performance Degradation**: Database operations slow down over time

## Risk Level
**CRITICAL** - This method is called frequently during tile rendering. Each tile generation involves multiple database queries, making this a high-frequency leak.

## Current Code

### GetTableData (Lines 69-79)
```csharp
private byte[]? GetTableData(ulong index, string name) {
    SqliteCommand sqlite = _sqliteConn.CreateCommand();  // ⚠️ Not disposed
    sqlite.CommandText = $"SELECT data FROM {name} WHERE position=@pos";
    sqlite.Parameters.Add(new SqliteParameter {
        ParameterName = "pos",
        DbType = DbType.UInt64,
        Value = index
    });
    using SqliteDataReader reader = sqlite.ExecuteReader();
    return reader.Read() ? reader["data"] as byte[] : null;
}
```

## Recommended Fix

```csharp
private byte[]? GetTableData(ulong index, string name) {
    using SqliteCommand sqlite = _sqliteConn.CreateCommand();  // ✅ Added using
    sqlite.CommandText = $"SELECT data FROM {name} WHERE position=@pos";
    sqlite.Parameters.Add(new SqliteParameter {
        ParameterName = "pos",
        DbType = DbType.UInt64,
        Value = index
    });
    using SqliteDataReader reader = sqlite.ExecuteReader();
    return reader.Read() ? reader["data"] as byte[] : null;
}
```

## Benefits of Fix
1. **Automatic Disposal**: Command is properly disposed when method exits
2. **No Resource Leaks**: Prevents accumulation of undisposed command objects
3. **Better Performance**: Releases database resources immediately
4. **Consistent Pattern**: Matches the existing pattern used for `SqliteDataReader`

## Testing Recommendations
1. Generate 1000+ tiles and monitor database connection count
2. Use SQL profiler to verify commands are being closed
3. Run extended rendering sessions and check for memory leaks
4. Monitor open handles using Process Explorer

## Additional Notes
- The code correctly uses `using` for `SqliteDataReader` but misses `SqliteCommand`
- This is a simple one-word fix with zero risk
- Should be prioritized due to high call frequency during tile rendering

## Related Issues
None

## References
- [Microsoft Docs: IDisposable Pattern](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
- [SQLite Connection Pooling](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings)
