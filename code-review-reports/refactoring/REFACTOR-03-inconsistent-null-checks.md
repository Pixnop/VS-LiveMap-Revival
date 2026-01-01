# REFACTOR-03: Inconsistent Null Checks

## Metadata
- **Issue ID**: REFACTOR-03
- **Priority**: Code Quality
- **Category**: Refactoring / Consistency
- **Status**: Open
- **Effort**: Easy

## Location
Multiple files use different null-checking patterns inconsistently.

## Description
The codebase uses a mix of null-checking styles:
- `if (obj == null) return;` (explicit)
- `obj?.Method()` (null-conditional)
- `obj != null` checks before use
- No checks at all (relying on nullable reference types)

This inconsistency makes the code harder to read and maintain.

## Examples

### Explicit Null Checks
```csharp
// BasicRenderer.cs:7
if (TileImage == null) {
    return;
}

// RenderTask.cs:22
if (region == null) {
    return;
}
```

### Null-Conditional Operators
```csharp
// LiveMap.cs:90
AsyncTaskManager?.Dispose();

// WebServer.cs:189
_listener?.Stop();
```

### Mixed Patterns in Same File
```csharp
// LiveMap.cs
AsyncTaskManager?.Dispose();  // Null-conditional
RenderTaskManager?.Dispose(); // Null-conditional

// But also:
if (_channel != null) {  // Explicit check
    _channel.SendPacket(...);
}
```

## Recommended Fix

### Establish Consistent Pattern

**For Early Returns:**
```csharp
// ✅ Use explicit null checks with early return
if (TileImage == null) {
    return;
}
```

**For Safe Calls:**
```csharp
// ✅ Use null-conditional for void methods
AsyncTaskManager?.Dispose();

// ✅ Use null-coalescing for fallbacks
var value = config?.Setting ?? defaultValue;
```

**For Assertions:**
```csharp
// ✅ Use null-forgiving for known non-null
var api = LiveMap.Api!;

// Or throw if unexpectedly null
var server = _server ?? throw new InvalidOperationException("Server not initialized");
```

### Guidelines Document

Create `docs/coding-standards.md`:
```markdown
## Null Handling

1. **Early Returns**: Use explicit null checks
   ```csharp
   if (obj == null) return;
   ```

2. **Safe Calls**: Use null-conditional operator
   ```csharp
   obj?.Method();
   ```

3. **Fallbacks**: Use null-coalescing
   ```csharp
   var value = obj ?? defaultValue;
   ```

4. **Never Null**: Use null-forgiving operator
   ```csharp
   var api = LiveMap.Api!;
   ```

5. **Enable Nullable Reference Types**: Already enabled
   ```xml
   <Nullable>enable</Nullable>
   ```
```

## Benefits
1. **Consistency**: Code is easier to read
2. **Predictability**: Developers know what to expect
3. **Fewer Bugs**: Clearer intent reduces mistakes
4. **Better Tooling**: Analyzers work better with consistent patterns

## Implementation
1. Document preferred patterns
2. Use code analyzer rules
3. Refactor incrementally during normal development
4. Add to code review checklist

## Recommended Analyzer Rules

```xml
<!-- .editorconfig -->
[*.cs]
# Prefer pattern matching for null checks
dotnet_style_prefer_is_null_check_over_reference_equality_method = true

# Prefer coalesce expression
dotnet_style_coalesce_expression = true

# Prefer null propagation
dotnet_style_null_propagation = true
```

## Related Issues
None

## References
- [C# Nullable Reference Types](https://docs.microsoft.com/en-us/dotnet/csharp/nullable-references)
- [Null-conditional Operators](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and-)
