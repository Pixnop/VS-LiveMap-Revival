# MEDIUM-01: Potential NPE - Nullable Chain

## Metadata
- **Issue ID**: MEDIUM-01
- **Severity**: MEDIUM
- **Category**: Bugs / TypeScript
- **Status**: Open
- **Effort**: Trivial

## Location
- **File**: `web/src/LiveMap.ts`
- **Line**: 72
- **Property**: `_scale`

## Description
The code uses the nullish coalescing assignment operator (`??=`) on line 72, but the `_scale` property is declared as `private readonly _scale: number` (line 32) without allowing `undefined`. This creates a type inconsistency.

## Impact
- **Type Safety Violation**: TypeScript compiler may not catch potential null/undefined issues
- **Runtime Confusion**: The `??=` operator is unnecessary if `_scale` can never be null/undefined
- **Maintainability**: Unclear whether `_scale` can be nullable or not

## Risk Level
**MEDIUM** - This is more of a code quality issue than a functional bug, but it indicates unclear nullability semantics.

## Current Code

### LiveMap Class (Lines 32, 72)
```typescript
export class LiveMap extends L.Map {
    // ...
    private readonly _scale: number;  // ⚠️ Declared as non-nullable

    constructor(settings: Settings) {
        // ...

        // pre-calculate map's scale
        this._scale ??= (1 / Math.pow(2, settings.zoom.maxout));  // ⚠️ Using ??=

        // ...
    }
}
```

## Recommended Fix

### Option 1: Remove Nullish Coalescing (Recommended)
```typescript
export class LiveMap extends L.Map {
    private readonly _scale: number;

    constructor(settings: Settings) {
        // ...

        // ✅ Direct assignment - _scale is never undefined
        this._scale = (1 / Math.pow(2, settings.zoom.maxout));

        // ...
    }
}
```

### Option 2: Make _scale Nullable (If Lazy Initialization Intended)
```typescript
export class LiveMap extends L.Map {
    private _scale?: number;  // ✅ Explicitly nullable

    constructor(settings: Settings) {
        // ...

        // ✅ Correct use of ??=
        this._scale ??= (1 / Math.pow(2, settings.zoom.maxout));

        // ...
    }

    get scale(): number {
        if (this._scale === undefined) {
            throw new Error('Scale not initialized');
        }
        return this._scale;
    }
}
```

## Benefits of Fix
1. **Type Clarity**: Clear whether `_scale` can be nullable
2. **Correct Operators**: Uses appropriate assignment operator
3. **Better Type Safety**: TypeScript can enforce nullability correctly
4. **Code Readability**: Intent is clearer

## Testing Recommendations
1. Verify TypeScript compiles without warnings
2. Enable `strictNullChecks` in tsconfig.json if not already enabled
3. Check that scale calculations work correctly at startup
4. Test with various zoom configurations

## Implementation Notes
- Option 1 is recommended since `_scale` is assigned in constructor and never reassigned
- The `readonly` keyword suggests `_scale` should be set once in constructor
- The nullish coalescing operator `??=` only assigns if the value is `null` or `undefined`
- Since this is a constructor assignment, simple `=` is more appropriate

## Related Issues
None

## References
- [TypeScript Handbook: Nullish Coalescing](https://www.typescriptlang.org/docs/handbook/release-notes/typescript-3-7.html#nullish-coalescing)
- [TypeScript strictNullChecks](https://www.typescriptlang.org/tsconfig#strictNullChecks)
