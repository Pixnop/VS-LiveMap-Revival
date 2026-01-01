# PERF-01: Unnecessary File Reads in WebServer

## Metadata
- **Issue ID**: PERF-01
- **Priority**: HIGH IMPACT
- **Category**: Performance / I/O
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `src/httpd/WebServer.cs`
- **Line**: 154
- **Method**: `HandleRequest()`

## Description
Every HTTP request reads the entire file from disk using `File.ReadAllBytes()`. For static assets (HTML, CSS, JS, images), this is extremely wasteful. Files are read repeatedly even though they rarely change.

## Impact
- **Disk I/O Overhead**: Every request hits the disk
- **Increased Latency**: File reads add 1-10ms per request
- **Cache Pollution**: OS page cache gets thrashed
- **CPU Waste**: Repeated file reads consume unnecessary cycles
- **Poor Scalability**: Server struggles under moderate traffic

### Performance Impact Estimation
- **Current**: ~5-15ms per request (file I/O)
- **With Caching**: ~0.1ms per request (memory lookup)
- **Improvement**: **50-150x faster** for static assets
- **High Traffic Benefit**: With 100 requests/sec, saves 500-1500ms of I/O per second

## Risk Level
**HIGH IMPACT** - This is the single biggest performance bottleneck in the web server. Static files account for most HTTP traffic (index.html, CSS, JS, tiles).

## Current Code

### HandleRequest (Lines 116-181)
```csharp
private static void HandleRequest(HttpListenerContext context) {
    string urlLoc = context.Request.Url?.LocalPath[1..] ?? "";

    // ... friendly URL parsing ...

    if (string.IsNullOrEmpty(urlLoc)) {
        urlLoc = "index.html";
    }

    using HttpListenerResponse response = context.Response;
    string filePath = Path.Combine(Files.WebDir, urlLoc);

    byte[] buffer;
    if (File.Exists(filePath)) {
        response.ContentType = MimeTypeMap.GetMimeType(new FileInfo(filePath).Extension) ?? MediaTypeNames.Text.Plain;
        buffer = File.ReadAllBytes(filePath);  // ⚠️ Reads from disk EVERY request
        response.StatusCode = 200;
    }
    else {
        response.ContentType = MediaTypeNames.Text.Html;
        buffer = File.ReadAllBytes(Path.Combine(Files.WebDir, "404.html"));  // ⚠️ Also reads 404 page every time
        response.StatusCode = 404;
    }

    // ... headers and response ...
}
```

## Recommended Fix

### Implementation with In-Memory Cache

```csharp
private static readonly ConcurrentDictionary<string, CachedFile> _fileCache = new();
private static FileSystemWatcher? _fileWatcher;

private record CachedFile(byte[] Data, string ContentType, long ETag);

private static void InitializeCache(string webDir) {
    // Set up file watcher to invalidate cache
    _fileWatcher = new FileSystemWatcher(webDir) {
        IncludeSubdirectories = true,
        EnableRaisingEvents = true,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
    };

    _fileWatcher.Changed += (s, e) => _fileCache.TryRemove(e.FullPath, out _);
    _fileWatcher.Created += (s, e) => _fileCache.TryRemove(e.FullPath, out _);
    _fileWatcher.Deleted += (s, e) => _fileCache.TryRemove(e.FullPath, out _);
    _fileWatcher.Renamed += (s, e) => {
        _fileCache.TryRemove(e.OldFullPath, out _);
        _fileCache.TryRemove(e.FullPath, out _);
    };
}

private static void HandleRequest(HttpListenerContext context) {
    string urlLoc = context.Request.Url?.LocalPath[1..] ?? "";

    // ... friendly URL parsing ...

    if (string.IsNullOrEmpty(urlLoc)) {
        urlLoc = "index.html";
    }

    using HttpListenerResponse response = context.Response;
    string filePath = Path.Combine(Files.WebDir, urlLoc);

    CachedFile? cachedFile;

    if (File.Exists(filePath)) {
        // ✅ Try to get from cache first
        cachedFile = _fileCache.GetOrAdd(filePath, path => {
            FileInfo fi = new(path);
            byte[] data = File.ReadAllBytes(path);
            string contentType = MimeTypeMap.GetMimeType(fi.Extension) ?? MediaTypeNames.Text.Plain;
            long etag = (long)(File.GetLastWriteTimeUtc(path) - DateTime.UnixEpoch).TotalMilliseconds;
            return new CachedFile(data, contentType, etag);
        });

        response.ContentType = cachedFile.ContentType;
        response.AddHeader("ETag", cachedFile.ETag.ToString());
        response.StatusCode = 200;
    }
    else {
        // ✅ Cache 404 page too
        string notFoundPath = Path.Combine(Files.WebDir, "404.html");
        cachedFile = _fileCache.GetOrAdd(notFoundPath, path => {
            byte[] data = File.ReadAllBytes(path);
            long etag = (long)(File.GetLastWriteTimeUtc(path) - DateTime.UnixEpoch).TotalMilliseconds;
            return new CachedFile(data, MediaTypeNames.Text.Html, etag);
        });

        response.ContentType = cachedFile.ContentType;
        response.StatusCode = 404;
    }

    // CORS headers
    response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
    response.AddHeader("Access-Control-Allow-Methods", "GET,POST");
    response.AddHeader("Access-Control-Allow-Origin", "*");

    // Write response
    response.ContentLength64 = cachedFile.Data.Length;
    response.OutputStream.Write(cachedFile.Data, 0, cachedFile.Data.Length);
    response.OutputStream.Close();
}

// In Dispose():
public void Dispose() {
    _stopped = true;
    // ... existing code ...
    _fileWatcher?.Dispose();
    _fileCache.Clear();
}
```

## Benefits of Fix
1. **Massive Speed Improvement**: 50-150x faster for cached files
2. **Reduced Disk I/O**: Files read once and cached
3. **Better Scalability**: Can handle much higher request rates
4. **Auto-Invalidation**: FileSystemWatcher updates cache when files change
5. **Memory Efficient**: Only caches accessed files
6. **ETag Support**: Proper HTTP caching headers

## Testing Recommendations
1. **Benchmark**: Measure request latency before/after with ApacheBench
   ```bash
   ab -n 1000 -c 10 http://localhost:PORT/
   ```
2. **Memory Test**: Monitor memory usage with many cached files
3. **Cache Invalidation**: Verify cache updates when files change
4. **Concurrent Access**: Test with multiple simultaneous requests
5. **Large Files**: Test with various file sizes (tiles can be 100KB+)

## Implementation Notes
- **Cache Size**: Consider adding max cache size or LRU eviction for very large deployments
- **Tiles**: Generated tiles change frequently; may want separate cache policy
- **ETag**: Current implementation uses LastWriteTime; cache should preserve this
- **Thread Safety**: ConcurrentDictionary is thread-safe for GetOrAdd
- **FileSystemWatcher**: Has some limitations; may need retry logic for missed events

## Alternative Approaches

### Option 2: Separate Cache for Static vs Dynamic Content
```csharp
// Cache static HTML/CSS/JS indefinitely
private static readonly ConcurrentDictionary<string, CachedFile> _staticCache = new();

// Cache tiles with TTL (they regenerate)
private static readonly ConcurrentDictionary<string, (CachedFile file, DateTime expires)> _tileCache = new();

private static CachedFile GetFile(string filePath) {
    if (filePath.Contains("/tiles/")) {
        // Short TTL for tiles
        if (_tileCache.TryGetValue(filePath, out var cached)) {
            if (cached.expires > DateTime.UtcNow) {
                return cached.file;
            }
            _tileCache.TryRemove(filePath, out _);
        }

        var file = LoadFile(filePath);
        _tileCache[filePath] = (file, DateTime.UtcNow.AddSeconds(30));
        return file;
    }

    // Permanent cache for static files
    return _staticCache.GetOrAdd(filePath, LoadFile);
}
```

## Caveats
- **Memory Usage**: Each cached file consumes memory (typical HTML ~50KB, tiles ~20KB)
- **Development**: May want to disable cache in dev builds for easier testing
- **Reload**: Config reload should clear cache to pick up new files

## Related Issues
None

## References
- [HTTP Caching Best Practices](https://developer.mozilla.org/en-US/docs/Web/HTTP/Caching)
- [FileSystemWatcher Limitations](https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher#remarks)
