# MEDIUM-02: Array Prototype Pollution

## Metadata
- **Issue ID**: MEDIUM-02
- **Severity**: MEDIUM
- **Category**: Bugs / JavaScript Best Practices
- **Status**: Open
- **Effort**: Easy

## Location
- **File**: `web/src/LiveMap.ts`
- **Lines**: 260-264
- **Method**: `Array.prototype.remove`

## Description
The code modifies the global `Array.prototype` by adding a custom `remove()` method. This is considered a bad practice (prototype pollution) as it affects all arrays in the application and can conflict with third-party libraries or future JavaScript features.

## Impact
- **Library Conflicts**: Third-party libraries may break if they rely on standard Array behavior
- **Future Compatibility**: May conflict with future ECMAScript Array methods
- **Unexpected Behavior**: All arrays in the app get this method, even in unrelated code
- **Type Safety Issues**: TypeScript may not recognize the custom method without declaration merging
- **Enumeration Issues**: Custom methods appear in `for...in` loops unless non-enumerable

## Risk Level
**MEDIUM** - While it currently works, this is a well-known anti-pattern that can cause subtle bugs, especially when integrating libraries like Leaflet, webpack, or other dependencies.

## Current Code

### Array.prototype.remove (Lines 260-264)
```typescript
// https://stackoverflow.com/a/3955096
Array.prototype.remove = function <T>(obj: T, ax?: number): void {
    while ((ax = this.indexOf(obj)) !== -1) {
        this.splice(ax, 1);
    }
};
```

## Recommended Fix

### Option 1: Utility Function (Recommended)
```typescript
// ✅ Create a utility function instead
export function removeFromArray<T>(array: T[], obj: T): void {
    let index: number;
    while ((index = array.indexOf(obj)) !== -1) {
        array.splice(index, 1);
    }
}

// Usage:
// Instead of: myArray.remove(item)
// Use: removeFromArray(myArray, item)
```

### Option 2: Array Extension Methods (ES6+ Style)
```typescript
// ✅ Create helper class with static methods
export class ArrayUtils {
    static remove<T>(array: T[], obj: T): void {
        let index: number;
        while ((index = array.indexOf(obj)) !== -1) {
            array.splice(index, 1);
        }
    }

    static removeAll<T>(array: T[], predicate: (item: T) => boolean): void {
        for (let i = array.length - 1; i >= 0; i--) {
            if (predicate(array[i])) {
                array.splice(i, 1);
            }
        }
    }
}

// Usage:
// ArrayUtils.remove(myArray, item)
```

### Option 3: Filter (Functional Approach)
```typescript
// ✅ Use built-in filter method
// Instead of mutating:
// myArray.remove(item)

// Create new array:
myArray = myArray.filter(x => x !== item);

// Or if mutation is needed:
function removeFromArray<T>(array: T[], obj: T): void {
    const filtered = array.filter(x => x !== obj);
    array.length = 0;
    array.push(...filtered);
}
```

## Migration Strategy

1. **Find All Usages**:
```bash
grep -r "\.remove(" web/src/
```

2. **Replace Calls**:
```typescript
// Before:
myArray.remove(item);

// After:
removeFromArray(myArray, item);
// or
ArrayUtils.remove(myArray, item);
```

3. **Remove Prototype Modification**:
```typescript
// Delete lines 260-264
```

4. **Update Type Declarations**:
Remove any `Array.prototype.remove` type declarations if they exist.

## Benefits of Fix
1. **No Prototype Pollution**: Doesn't modify global objects
2. **Library Safe**: No conflicts with third-party code
3. **Type Safe**: Standard TypeScript typing
4. **Future Proof**: Won't conflict with future Array methods
5. **Testable**: Easier to unit test utility functions
6. **Explicit**: Makes it clear when array mutation happens

## Testing Recommendations
1. Search for all `.remove(` calls and verify they're updated
2. Test with Leaflet library to ensure no regressions
3. Run TypeScript compiler with strict mode
4. Test array operations across the application
5. Verify webpack bundling still works correctly

## Implementation Notes
- The current implementation removes ALL occurrences of `obj` from the array
- Consider if this is always desired behavior (vs removing first occurrence only)
- The functional `filter` approach is more idiomatic in modern JavaScript
- If you must keep prototype extension, at least make it non-enumerable:

```typescript
Object.defineProperty(Array.prototype, 'remove', {
    value: function<T>(obj: T): void { /* ... */ },
    enumerable: false,  // Won't appear in for...in
    writable: false,
    configurable: false
});
```

## Related Issues
None

## References
- [Why Extending Native Objects is Bad](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Inheritance_and_the_prototype_chain#bad_practice_extension_of_native_prototypes)
- [ESLint no-extend-native Rule](https://eslint.org/docs/latest/rules/no-extend-native)
- [You Don't Know JS: Prototypes](https://github.com/getify/You-Dont-Know-JS/blob/1st-ed/this%20%26%20object%20prototypes/ch5.md)
