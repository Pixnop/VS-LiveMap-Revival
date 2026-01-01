# MEDIUM-03: Empty Catch Blocks

## Metadata
- **Issue ID**: MEDIUM-03
- **Severity**: MEDIUM
- **Category**: Bugs / Error Handling
- **Status**: Open
- **Effort**: Easy

## Location
Multiple files contain empty catch blocks that silently swallow exceptions:

### C# Files:
1. **src/httpd/WebServer.cs**: Lines 57, 88, 100, 104, 137, 173
2. **src/task/RenderTaskManager.cs**: Lines 111, 201
3. **src/data/ChunkLoader.cs**: Line 86

## Description
Empty `catch` blocks with only `// ignore` comments suppress all exceptions without any logging or recovery. This makes debugging extremely difficult as errors fail silently.

## Impact
- **Silent Failures**: Errors occur with no visibility
- **Difficult Debugging**: No stack traces or error messages
- **Masked Bugs**: Underlying issues go unnoticed
- **Production Issues**: Problems only discovered by users reporting broken functionality
- **No Telemetry**: Can't track error frequency or patterns

## Risk Level
**MEDIUM** - While some exceptions may be legitimately ignorable, the current approach provides zero observability.

## Current Code

### WebServer.cs - Multiple Locations

#### Lines 57-59 (Fallback listener creation)
```csharp
try {
    (_listener = new HttpListener { Prefixes = { $"http://*:{port}/" } }).Start();
}
catch (Exception) {  // ⚠️ Empty catch
    Logger.Warn($"Internal webserver failed to bind...");
    // Fallback code
}
```

#### Lines 88-111 (Request handling)
```csharp
try {
    HandleRequest(_listener!.GetContext());
}
catch (Exception) {  // ⚠️ Empty catch - completely silent
    if (_stopped) {
        Logger.Info("Internal webserver has stopped");
        // ...
    }
    // Exception details lost!
}
```

#### Lines 100-107 (Listener stop)
```csharp
try {
    _listener?.Stop();
}
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

#### Lines 137-140 (Friendly URL parsing)
```csharp
try {
    // Regex matching
}
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

#### Lines 173-176 (ETag generation)
```csharp
try {
    TimeSpan time = File.GetLastWriteTimeUtc(filePath) - DateTime.UnixEpoch;
    response.AddHeader("ETag", ((long)time.TotalMilliseconds).ToString());
}
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

### RenderTaskManager.cs

#### Lines 111-113 (Render task loop)
```csharp
try {
    while (_running) {
        // Process regions
    }
}
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

#### Lines 201-203 (Block column scan)
```csharp
try {
    y = GetTopBlockY(mapChunk, x, z);
    // ... get blocks
}
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

### ChunkLoader.cs

#### Lines 86-88 (Dispose)
```csharp
catch (Exception) {  // ⚠️ Empty catch
    // ignore
}
```

## Recommended Fix

### General Pattern
```csharp
catch (Exception e) {
    Logger.Debug($"Expected exception during [operation]: {e.Message}");
    // OR
    Logger.Warn($"Non-critical error in [operation]: {e.Message}");
    // OR for truly expected exceptions:
    // Silently ignore, but document WHY
}
```

### Specific Fixes

#### WebServer.cs Line 88 (Request Handling)
```csharp
try {
    HandleRequest(_listener!.GetContext());
}
catch (HttpListenerException e) when (_stopped) {
    // Expected when stopping listener
    Logger.Debug($"Listener stopped: {e.Message}");
}
catch (Exception e) {
    Logger.Error($"Unexpected error handling HTTP request: {e}");
}
finally {
    if (_stopped) {
        Logger.Info("Internal webserver has stopped");
        if (_reload) {
            _reload = false;
            _stopped = false;
        }
    }

    try {
        _listener?.Stop();
    }
    catch (ObjectDisposedException) {
        // Expected during shutdown
    }

    _running = false;
    Thread.CurrentThread.Interrupt();
}
```

#### WebServer.cs Line 137 (Friendly URL Parsing)
```csharp
try {
    MatchCollection matches = FriendlyUrlRegex().Matches(urlLoc);
    if (matches.Count > 0) {
        string group6 = matches[0].Groups[6].Value;
        if (group6.Length == 0 && !matches[0].Value.EndsWith('/')) {
            context.Response.Redirect($"{context.Request.Url?.OriginalString}/");
            context.Response.Close();
            return;
        }
        urlLoc = group6[1..];
    }
}
catch (ArgumentException e) {
    Logger.Warn($"Invalid URL pattern: {urlLoc} - {e.Message}");
    // Fall through to default handling
}
```

#### WebServer.cs Line 173 (ETag Generation)
```csharp
catch (IOException e) {
    Logger.Debug($"Could not generate ETag for {filePath}: {e.Message}");
    // ETag is optional, continue without it
}
```

#### RenderTaskManager.cs Line 111
```csharp
catch (InvalidOperationException e) when (_processQueue.IsCompleted) {
    Logger.Debug($"Render queue completed: {e.Message}");
}
catch (Exception e) {
    Logger.Error($"Error processing render queue: {e}");
}
```

#### RenderTaskManager.cs Line 201 (RenderTask.cs)
```csharp
catch (IndexOutOfRangeException e) {
    Logger.Warn($"Block access out of range at {x},{z}: {e.Message}");
    // Return default BlockData
}
catch (Exception e) {
    Logger.Error($"Unexpected error scanning block column at {x},{z}: {e}");
    // Return default BlockData
}
```

#### ChunkLoader.cs Line 86
```csharp
catch (SqliteException e) {
    Logger.Warn($"Error closing SQLite connection: {e.Message}");
}
catch (InvalidOperationException e) {
    Logger.Debug($"ChunkDataPool already disposed: {e.Message}");
}
```

## Benefits of Fix
1. **Visibility**: Errors are logged and can be diagnosed
2. **Debugging**: Stack traces available when issues occur
3. **Monitoring**: Can track error frequency and patterns
4. **Specific Handling**: Catch specific exceptions rather than all
5. **Better Code**: Documents which exceptions are expected and why

## Testing Recommendations
1. Trigger each error path and verify appropriate logging
2. Check that legitimate errors don't spam logs
3. Verify application still functions when exceptions occur
4. Review logs after testing to ensure clarity
5. Test error scenarios: network failures, file locks, etc.

## Implementation Notes
- Use specific exception types when possible (e.g., `IOException`, `SqliteException`)
- Log at appropriate levels: `Debug` for expected, `Warn` for recoverable, `Error` for unexpected
- Document WHY exceptions are being ignored if truly intentional
- Consider whether recovery logic is needed beyond just logging
- Use `when` clauses to catch exceptions only in specific conditions

## Related Issues
- See HIGH-03: Inconsistent Error Logging (use Logger consistently)

## References
- [C# Exception Handling Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions)
- [CA1031: Do not catch general exception types](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1031)
