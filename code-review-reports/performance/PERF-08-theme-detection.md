# PERF-08: Theme Detection on Every Load

## Metadata
- **Issue ID**: PERF-08
- **Priority**: LOW IMPACT
- **Category**: Performance / Frontend Initialization
- **Status**: Open
- **Effort**: Easy

## Location
- **File**: `web/src/LiveMap.ts`
- **Lines**: 269-282
- **Code**: Theme detection loop

## Description
The code iterates through all stylesheets and all CSS rules to find available themes on every page load. This work is completely static (themes don't change at runtime) but gets repeated every time.

## Impact
- **Slow Page Load**: Adds 1-5ms to initialization
- **Wasted CPU**: Parsing rules unnecessarily
- **Code Complexity**: More complex than needed

### Performance Impact Estimation
- **Current**: ~1-5ms per page load (iterating stylesheets)
- **Optimized**: ~0.1ms (localStorage lookup)
- **Improvement**: ~10-50x faster initialization

## Risk Level
**LOW IMPACT** - Only runs once per page load, minimal impact. But easy to fix.

## Current Code

### Theme Detection (Lines 267-282)
```typescript
const knownThemes: string[] = [];

// ⚠️ Runs on every page load
for (let i: number = 0; i < document.styleSheets.length; i++) {
    const css: CSSStyleSheet = document.styleSheets[i];
    if (css.href?.endsWith('themes.css')) {
        const rules: CSSRuleList = css.cssRules;
        for (let j: number = 0; j < rules.length; j++) {
            const rule: CSSStyleRule = rules[j] as CSSStyleRule;
            const match: RegExpExecArray | null = /html\[theme=\u0022(.+)\u0022]/.exec(rule.selectorText);
            if (match) {
                knownThemes.push(match[1]);
            }
        }
        break;
    }
}
```

## Recommended Fix

### Option 1: Hardcode Theme List (Simplest)
```typescript
// ✅ Themes are known at build time
const knownThemes: string[] = ['light', 'dark'];

const setTheme = (): void => {
    const prefersDark: boolean = knownThemes.length > 1 && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme: string = localStorage.getItem('theme') ?? knownThemes[+prefersDark];
    document.querySelector('html')!.setAttribute('theme', theme);

    const icon: HTMLLinkElement | null = document.querySelector(`link[rel='shortcut icon']`);
    if (icon) {
        icon.href = prefersDark ? 'favicon-white.ico' : 'favicon.ico';
    }
};
```

### Option 2: Cache in LocalStorage
```typescript
const knownThemes: string[] = (() => {
    // ✅ Try to load from cache first
    const cached = localStorage.getItem('livemap:knownThemes');
    if (cached) {
        try {
            return JSON.parse(cached);
        } catch {
            // Fall through to detection
        }
    }

    // ⚠️ Only detect if not cached
    const themes: string[] = [];
    for (let i: number = 0; i < document.styleSheets.length; i++) {
        const css: CSSStyleSheet = document.styleSheets[i];
        if (css.href?.endsWith('themes.css')) {
            const rules: CSSRuleList = css.cssRules;
            for (let j: number = 0; j < rules.length; j++) {
                const rule: CSSStyleRule = rules[j] as CSSStyleRule;
                const match: RegExpExecArray | null = /html\[theme=\u0022(.+)\u0022]/.exec(rule.selectorText);
                if (match) {
                    themes.push(match[1]);
                }
            }
            break;
        }
    }

    // ✅ Cache for next time
    if (themes.length > 0) {
        localStorage.setItem('livemap:knownThemes', JSON.stringify(themes));
    }

    return themes;
})();
```

### Option 3: Build-Time Generation
In webpack.config.js, generate a themes list:
```javascript
// webpack.config.js
const fs = require('fs');
const cssContent = fs.readFileSync('src/scss/themes.scss', 'utf-8');
const themeMatches = cssContent.matchAll(/html\[theme="(.+?)"\]/g);
const themes = Array.from(themeMatches, m => m[1]);

// Inject into bundle
plugins: [
    new webpack.DefinePlugin({
        __AVAILABLE_THEMES__: JSON.stringify(themes)
    })
]

// In TypeScript:
declare const __AVAILABLE_THEMES__: string[];
const knownThemes: string[] = __AVAILABLE_THEMES__;
```

## Benefits of Fix
1. **Faster Load Time**: Eliminates stylesheet parsing
2. **Simpler Code**: Hardcoded list is clearer
3. **No Runtime Parsing**: Work done at build time
4. **Type Safety**: Can declare theme union type

## Testing Recommendations
1. Verify theme switching still works
2. Test with both light and dark modes
3. Test localStorage persistence
4. Verify favicon changes correctly
5. Test with browser theme preference changes

## Implementation Notes
- **Option 1** is recommended: themes rarely change
- If themes are dynamic, use Option 2
- Option 3 is overkill but provides best type safety
- Consider making theme names a TypeScript enum:
```typescript
enum Theme {
    Light = 'light',
    Dark = 'dark'
}
```

## Alternative Approaches

### CSS Variables Approach
```typescript
// Instead of detecting themes, use CSS custom properties
const theme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
document.documentElement.setAttribute('theme', theme);

// CSS:
// html[theme="dark"] {
//   --bg-color: #000;
//   --text-color: #fff;
// }
// html[theme="light"] {
//   --bg-color: #fff;
//   --text-color: #000;
// }
```

## Caveats
- Hardcoding assumes themes won't change without code update
- LocalStorage can be cleared by users
- Build-time generation requires rebuild for theme changes

## Related Issues
None

## References
- [CSS Custom Properties](https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties)
- [LocalStorage API](https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage)
- [Webpack DefinePlugin](https://webpack.js.org/plugins/define-plugin/)
