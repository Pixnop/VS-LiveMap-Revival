# PERF-05: Polling Loop in Frontend

## Metadata
- **Issue ID**: PERF-05
- **Priority**: MEDIUM IMPACT
- **Category**: Performance / Frontend
- **Status**: Open
- **Effort**: Medium

## Location
- **File**: `web/src/LiveMap.ts`
- **Lines**: 185-196
- **Method**: `loop()`

## Description
The frontend uses `setTimeout` with a fixed 1000ms interval to update layers and tiles. This runs continuously regardless of whether there's actual work to do or if the browser tab is visible.

## Impact
- **Battery Drain**: Unnecessary updates on mobile devices
- **CPU Waste**: Processing when tab is hidden
- **Network Overhead**: Polling for data that hasn't changed
- **Poor UX**: Fixed interval may miss rapid updates or waste cycles on slow updates

### Performance Impact Estimation
- **Current**: 1 update/second regardless of need
- **Optimized**: Update only when needed + respect visibility
- **Improvement**: ~50-90% reduction in CPU usage when idle

## Risk Level
**MEDIUM IMPACT** - Affects all frontend users. Particularly important for mobile and battery life.

## Current Code

### Loop Method (Lines 185-196)
```typescript
private loop(count: number): void {
    try {
        // ⚠️ Only skips if not visible, but still schedules timer
        if (document.visibilityState === 'visible') {
            this.tileLayerControl.tick(count);
            this.layersControl.tick(count);
        }
    } catch (e) {
        console.error(`Error processing tick (${count})\n`, e);
    }

    // ⚠️ Always schedules next tick after 1000ms
    setTimeout(() => this.loop(++count), 1000);
}
```

## Recommended Fix

### Option 1: Adaptive Polling with Visibility API (Recommended)
```typescript
private _loopIntervalId?: number;
private _tickInterval: number = 1000;

private loop(count: number): void {
    try {
        if (document.visibilityState === 'visible') {
            this.tileLayerControl.tick(count);
            this.layersControl.tick(count);
        }
    } catch (e) {
        console.error(`Error processing tick (${count})\n`, e);
    }

    // ✅ Adapt interval based on visibility
    const interval = document.visibilityState === 'visible' ? this._tickInterval : 5000;
    this._loopIntervalId = window.setTimeout(() => this.loop(++count), interval);
}

// ✅ Add cleanup method
public destroy(): void {
    if (this._loopIntervalId !== undefined) {
        window.clearTimeout(this._loopIntervalId);
    }
}
```

### Option 2: Event-Driven Updates (Better Performance)
```typescript
private _animationFrameId?: number;
private _lastUpdate: number = 0;
private _updateInterval: number = 1000;

private loop(count: number): void {
    const now = performance.now();

    // ✅ Only update at specified interval
    if (now - this._lastUpdate >= this._updateInterval) {
        try {
            if (document.visibilityState === 'visible') {
                this.tileLayerControl.tick(count);
                this.layersControl.tick(count);
            }
        } catch (e) {
            console.error(`Error processing tick (${count})\n`, e);
        }
        this._lastUpdate = now;
    }

    // ✅ Use requestAnimationFrame for smooth updates
    this._animationFrameId = window.requestAnimationFrame(() => this.loop(count + 1));
}

public destroy(): void {
    if (this._animationFrameId !== undefined) {
        window.cancelAnimationFrame(this._animationFrameId);
    }
}
```

### Option 3: WebSocket Push Updates (Best UX)
```typescript
private _ws?: WebSocket;
private _pollingFallback?: number;

constructor(settings: Settings) {
    // ... existing code ...

    // ✅ Try WebSocket first
    this.initializeWebSocket(settings);

    // ✅ Fallback to polling if WebSocket fails
    this._pollingFallback = window.setTimeout(() => {
        if (!this._ws || this._ws.readyState !== WebSocket.OPEN) {
            this.startPolling();
        }
    }, 5000);
}

private initializeWebSocket(settings: Settings): void {
    try {
        const wsUrl = `ws://${window.location.host}/ws/updates`;
        this._ws = new WebSocket(wsUrl);

        this._ws.onmessage = (event) => {
            const data = JSON.parse(event.data);
            if (data.type === 'tile_update') {
                this.tileLayerControl.handleUpdate(data);
            } else if (data.type === 'layer_update') {
                this.layersControl.handleUpdate(data);
            }
        };

        this._ws.onerror = () => {
            console.warn('WebSocket failed, falling back to polling');
            this.startPolling();
        };
    } catch (e) {
        console.warn('WebSocket not supported, using polling');
        this.startPolling();
    }
}

private startPolling(): void {
    // Fall back to current polling mechanism
    setTimeout(() => this.loop(0), 1000);
}
```

## Benefits of Fix
1. **Better Battery Life**: Reduced CPU usage when idle
2. **Responsive**: requestAnimationFrame syncs with browser refresh
3. **Adaptive**: Slows down or stops when tab hidden
4. **Push Updates**: WebSocket option provides real-time updates
5. **Resource Efficient**: Only processes when needed

## Testing Recommendations
1. Test with browser tab in background - verify reduced activity
2. Monitor CPU usage in dev tools
3. Test on mobile devices for battery impact
4. Verify updates still occur at appropriate rate
5. Test WebSocket reconnection logic

## Implementation Notes
- **Option 1**: Easy upgrade, minimal changes
- **Option 2**: Better for animation-heavy scenarios
- **Option 3**: Best UX but requires server-side WebSocket support
- **Visibility API**: Supported in all modern browsers
- **requestAnimationFrame**: Automatically pauses when tab hidden

## Server-Side Changes for WebSocket (Option 3)

Would require adding WebSocket endpoint to WebServer.cs:
```csharp
// In WebServer.cs
private readonly Dictionary<string, WebSocket> _wsClients = new();

private async Task HandleWebSocketRequest(HttpListenerContext context) {
    if (context.Request.IsWebSocketRequest) {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;

        string clientId = Guid.NewGuid().ToString();
        _wsClients[clientId] = ws;

        // Send updates when tiles/layers change
        // Remove from _wsClients when disconnected
    }
}
```

## Alternative Approaches

### Hybrid: Poll Less Frequently, Push Critical Updates
```typescript
// Poll every 5 seconds for general updates
private _slowPollInterval = 5000;

// But allow immediate updates via custom events
window.addEventListener('livemap:forceUpdate', () => {
    this.tileLayerControl.tick(0);
    this.layersControl.tick(0);
});

// Modules can trigger: window.dispatchEvent(new Event('livemap:forceUpdate'))
```

## Caveats
- WebSocket requires server changes
- requestAnimationFrame may run at 60fps vs 1fps - need throttling
- Visibility API is well-supported but check compatibility requirements

## Related Issues
None

## References
- [Page Visibility API](https://developer.mozilla.org/en-US/docs/Web/API/Page_Visibility_API)
- [requestAnimationFrame](https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame)
- [WebSocket API](https://developer.mozilla.org/en-US/docs/Web/API/WebSocket)
