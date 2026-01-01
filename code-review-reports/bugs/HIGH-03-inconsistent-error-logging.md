# HIGH-03: Inconsistent Error Handling

## Metadata
- **Issue ID**: HIGH-03
- **Severity**: HIGH
- **Category**: Bugs / Logging
- **Status**: Open
- **Effort**: Easy

## Location
Multiple files use `Console.Error.WriteLine()` instead of the project's `Logger` class:

1. **src/task/AsyncTaskManager.cs**: Lines 17, 28
2. **src/task/AsyncTask.cs**: Line 19
3. **src/task/MarkersTask.cs**: Line 40
4. **src/task/SettingsTask.cs**: Line 78
5. **src/layer/marker/Marker.cs**: Line 66

## Description
The codebase has an established `Logger` utility for logging (used extensively elsewhere), but 6 locations bypass it and write directly to `Console.Error`. This creates inconsistent logging that won't be captured by the game's logging system.

## Impact
- **Lost Error Messages**: Console.Error output may not appear in game logs
- **No Log Levels**: Cannot filter by severity
- **No Timestamps**: Console.Error doesn't include timestamps
- **Difficult Debugging**: Errors not visible in server log files
- **No Context**: Missing mod identifier and structured formatting

## Risk Level
**HIGH** - Errors in async tasks and marker parsing will be silently lost, making production issues very difficult to diagnose.

## Current Code

### AsyncTaskManager.cs (Lines 11-20, 22-30)
```csharp
public void Tick() {
    foreach (AsyncTask task in _tasks) {
        try {
            task.Tick();
        }
        catch (Exception e) {
            Console.Error.WriteLine(e.ToString());  // ⚠️ Direct console write
        }
    }
}

public void Dispose() {
    foreach (AsyncTask task in _tasks) {
        try {
            task.Dispose();
        }
        catch (Exception e) {
            Console.Error.WriteLine(e.ToString());  // ⚠️ Direct console write
        }
    }
}
```

### AsyncTask.cs (Lines 15-20)
```csharp
protected async Task RunAsync(CancellationToken cancellationToken) {
    try {
        await Update();
    }
    catch (Exception e) {
        await Console.Error.WriteLineAsync(e.ToString());  // ⚠️ Async console write
    }
}
```

### MarkersTask.cs (Lines 36-41)
```csharp
try {
    await WriteAsync(json);
}
catch (Exception e) {
    await Console.Error.WriteLineAsync(e.ToString());  // ⚠️ Async console write
}
```

### SettingsTask.cs (Line 78)
```csharp
catch (Exception e) {
    await Console.Error.WriteLineAsync(e.ToString());  // ⚠️ Async console write
}
```

### Marker.cs (Line 66)
```csharp
catch (Exception e) {
    Console.Error.WriteLine($"Error deserializing marker json ({json})");  // ⚠️ Direct console write
    Console.Error.WriteLine(e.ToString());  // ⚠️ Direct console write
}
```

## Recommended Fix

### AsyncTaskManager.cs
```csharp
public void Tick() {
    foreach (AsyncTask task in _tasks) {
        try {
            task.Tick();
        }
        catch (Exception e) {
            Logger.Error($"Error ticking async task: {e}");  // ✅ Use Logger
        }
    }
}

public void Dispose() {
    foreach (AsyncTask task in _tasks) {
        try {
            task.Dispose();
        }
        catch (Exception e) {
            Logger.Error($"Error disposing async task: {e}");  // ✅ Use Logger
        }
    }
}
```

### AsyncTask.cs
```csharp
protected async Task RunAsync(CancellationToken cancellationToken) {
    try {
        await Update();
    }
    catch (Exception e) {
        Logger.Error($"Error in async task update: {e}");  // ✅ Use Logger (synchronous)
    }
}
```

### MarkersTask.cs
```csharp
try {
    await WriteAsync(json);
}
catch (Exception e) {
    Logger.Error($"Error writing markers: {e}");  // ✅ Use Logger
}
```

### SettingsTask.cs
```csharp
catch (Exception e) {
    Logger.Error($"Error writing settings: {e}");  // ✅ Use Logger
}
```

### Marker.cs
```csharp
catch (Exception e) {
    Logger.Error($"Error deserializing marker json: {json}");  // ✅ Use Logger
    Logger.Error(e.ToString());
}
```

## Benefits of Fix
1. **Centralized Logging**: All errors go through Logger class
2. **Consistent Format**: Includes timestamps, mod ID, log levels
3. **Configurable Output**: Can be redirected to files, filtered by level
4. **Searchable Logs**: Easier to grep/search server logs
5. **Production Visibility**: Errors appear in game's logging system

## Testing Recommendations
1. Trigger errors in async tasks and verify they appear in game logs
2. Check that error messages include proper context
3. Verify log file contains error entries with timestamps
4. Test that log level filtering works correctly
5. Ensure no duplicate error messages

## Implementation Notes
- `Logger.Error()` is synchronous, so remove `await` for console writes
- Add descriptive prefixes to error messages for better context
- Consider adding task type information to AsyncTaskManager errors
- May want to add stack traces for debugging (already included in `e.ToString()`)

## Related Issues
- See MEDIUM-03: Empty Catch Blocks (related logging issue)

## References
- [Logging Best Practices](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging-best-practices)
- Check `src/util/Logger.cs` for available logging methods
