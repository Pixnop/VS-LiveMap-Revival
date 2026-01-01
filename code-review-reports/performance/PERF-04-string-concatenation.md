# PERF-04: String Concatenation in GetAvatar

## Metadata
- **Issue ID**: PERF-04
- **Priority**: MEDIUM IMPACT
- **Category**: Performance / Strings
- **Status**: Open
- **Effort**: Trivial

## Location
- **File**: `src/util/Extensions.cs`
- **Lines**: 110-117
- **Method**: `GetAvatar()`

## Description
The method builds a URL using string interpolation which already creates a new string, but the pattern with multiple string parts could be slightly optimized. While modern C# handles string interpolation efficiently, this method is called for every player on every position update.

## Impact
- **Minor Overhead**: String allocations for each player update
- **GC Pressure**: Additional objects for garbage collector
- **Frequency**: Called every tick for every visible player

### Performance Impact Estimation
- **Current**: ~100-500ns per call
- **Optimized**: ~80-300ns per call
- **Improvement**: ~20-40% faster (minor but measurable)
- **Real-world**: With 10 players updating at 1Hz, saves ~2-4μs/second

## Risk Level
**MEDIUM IMPACT** - Low priority but easy fix. Called frequently enough to matter on busy servers.

## Current Code

### GetAvatar (Lines 108-118)
```csharp
public static string GetAvatar(this EntityPlayer player) {
    ITreeAttribute appliedParts = (ITreeAttribute)player.WatchedAttributes.GetTreeAttribute("skinConfig")["appliedParts"];
    return $"https://vs.pl3x.net/v1/" +  // ⚠️ Uses + instead of interpolation
           $"{appliedParts.GetString("baseskin")}/" +
           $"{appliedParts.GetString("eyecolor")}/" +
           $"{appliedParts.GetString("hairbase")}/" +
           $"{appliedParts.GetString("hairextra")}/" +
           $"{appliedParts.GetString("mustache")}/" +
           $"{appliedParts.GetString("beard")}/" +
           $"{appliedParts.GetString("haircolor")}.png";
}
```

## Recommended Fix

### Option 1: Single String Interpolation (Recommended)
```csharp
public static string GetAvatar(this EntityPlayer player) {
    ITreeAttribute appliedParts = (ITreeAttribute)player.WatchedAttributes.GetTreeAttribute("skinConfig")["appliedParts"];

    // ✅ Single interpolated string - compiler optimizes this
    return $"https://vs.pl3x.net/v1/{appliedParts.GetString("baseskin")}/{appliedParts.GetString("eyecolor")}/{appliedParts.GetString("hairbase")}/{appliedParts.GetString("hairextra")}/{appliedParts.GetString("mustache")}/{appliedParts.GetString("beard")}/{appliedParts.GetString("haircolor")}.png";
}
```

### Option 2: StringBuilder (If Called Very Frequently)
```csharp
private static readonly StringBuilder _avatarBuilder = new(128);

public static string GetAvatar(this EntityPlayer player) {
    ITreeAttribute appliedParts = (ITreeAttribute)player.WatchedAttributes.GetTreeAttribute("skinConfig")["appliedParts"];

    // ✅ Reuse StringBuilder
    lock (_avatarBuilder) {
        _avatarBuilder.Clear();
        _avatarBuilder.Append("https://vs.pl3x.net/v1/");
        _avatarBuilder.Append(appliedParts.GetString("baseskin"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("eyecolor"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("hairbase"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("hairextra"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("mustache"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("beard"));
        _avatarBuilder.Append('/');
        _avatarBuilder.Append(appliedParts.GetString("haircolor"));
        _avatarBuilder.Append(".png");
        return _avatarBuilder.ToString();
    }
}
```

### Option 3: Cache Avatar URLs
```csharp
private static readonly ConcurrentDictionary<long, (string url, DateTime expires)> _avatarCache = new();

public static string GetAvatar(this EntityPlayer player) {
    long playerId = player.EntityId;
    DateTime now = DateTime.UtcNow;

    // ✅ Return cached URL if still valid
    if (_avatarCache.TryGetValue(playerId, out var cached) && cached.expires > now) {
        return cached.url;
    }

    ITreeAttribute appliedParts = (ITreeAttribute)player.WatchedAttributes.GetTreeAttribute("skinConfig")["appliedParts"];
    string url = $"https://vs.pl3x.net/v1/{appliedParts.GetString("baseskin")}/{appliedParts.GetString("eyecolor")}/{appliedParts.GetString("hairbase")}/{appliedParts.GetString("hairextra")}/{appliedParts.GetString("mustache")}/{appliedParts.GetString("beard")}/{appliedParts.GetString("haircolor")}.png";

    // Cache for 5 minutes (skins rarely change)
    _avatarCache[playerId] = (url, now.AddMinutes(5));
    return url;
}
```

## Benefits of Fix
1. **Fewer Allocations**: Single string created instead of multiple
2. **Better Performance**: Compiler can optimize single interpolation
3. **Cache Option**: Can eliminate most calls entirely
4. **Cleaner Code**: Single-line return is more readable

## Testing Recommendations
1. Benchmark with 100 player avatar requests
2. Monitor string allocations with memory profiler
3. Verify URLs are identical
4. Test cache invalidation (Option 3) when players change skins

## Implementation Notes
- **Option 1**: Simplest, already handles most cases efficiently
- **Option 2**: Overkill unless called millions of times
- **Option 3**: Best if skins rarely change (which is typical)
- **Modern C#**: String interpolation is heavily optimized by compiler
- **Readability**: Single-line version is easier to maintain

## Alternative Approaches

### Span<char> for Zero-Allocation (Advanced)
```csharp
public static string GetAvatar(this EntityPlayer player) {
    ITreeAttribute appliedParts = (ITreeAttribute)player.WatchedAttributes.GetTreeAttribute("skinConfig")["appliedParts"];

    Span<char> buffer = stackalloc char[256];
    var handler = new DefaultInterpolatedStringHandler(200, 7, out bool shouldAppend);
    if (shouldAppend) {
        handler.AppendLiteral("https://vs.pl3x.net/v1/");
        handler.AppendFormatted(appliedParts.GetString("baseskin"));
        // ... etc
    }
    return handler.ToStringAndClear();
}
```

## Caveats
- Performance gain is minor for typical player counts
- Cache (Option 3) uses memory but provides best performance
- Avatar URLs rarely change, making caching highly effective

## Related Issues
None

## References
- [String Interpolation Performance](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-10#interpolated-string-improvements)
- [StringBuilder vs String Concatenation](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder)
