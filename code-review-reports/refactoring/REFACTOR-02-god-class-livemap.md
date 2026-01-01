# REFACTOR-02: God Class - LiveMap.cs

## Metadata
- **Issue ID**: REFACTOR-02
- **Priority**: Code Quality
- **Category**: Refactoring / Architecture
- **Status**: Open
- **Effort**: Hard

## Location
- **File**: `src/LiveMap.cs`
- **Lines**: 1-190 (entire file)

## Description
The `LiveMap` class has 190 lines and handles too many responsibilities: initialization, configuration management, event handling, networking, task management, registry management, and disposal. This violates the Single Responsibility Principle.

## Impact
- **Hard to Test**: Too many dependencies
- **Hard to Maintain**: Changes affect multiple concerns
- **Hard to Understand**: Unclear primary purpose
- **Tight Coupling**: Everything depends on LiveMap

## Current Responsibilities
1. Configuration loading/saving (lines 89-118)
2. Registry initialization (lines 64-66, 78-79)
3. Task manager lifecycle (lines 68-69, 90-94, 101-102)
4. Web server management (line 70)
5. Game event handling (lines 72-73, 124-132, 135-144)
6. Network packet handling (lines 84-86, 120-122, 146-162)
7. Disposal (lines 164-189)

## Recommended Fix

### Extract ConfigManager
```csharp
public class ConfigManager {
    private readonly LiveMap _server;
    private readonly FileWatcher _watcher;

    public Config Config { get; private set; }

    public ConfigManager(LiveMap server) {
        _server = server;
        _watcher = new FileWatcher(server, this);
        Load();
    }

    public void Load() {
        Config = _server.Sapi.LoadModConfig<Config>($"{_server.ModId}.json") ?? new Config();
    }

    public void Save() {
        // ... save logic ...
    }

    public void Reload() {
        Load();
        Save();
    }
}
```

### Extract EventCoordinator
```csharp
public class EventCoordinator {
    private readonly LiveMap _server;
    private long _gameTickTaskId;

    public EventCoordinator(LiveMap server) {
        _server = server;
        RegisterEventHandlers();
    }

    private void RegisterEventHandlers() {
        _server.Sapi.Event.ChunkDirty += OnChunkDirty;
        _server.Sapi.Event.GameWorldSave += OnGameWorldSave;
        _gameTickTaskId = _server.Sapi.Event.RegisterGameTickListener(OnGameTick, 1000, 1000);
    }

    private void OnChunkDirty(Vec3i chunkCoord, IWorldChunk chunk, EnumChunkDirtyReason reason) {
        _server.RenderTaskManager?.Queue(chunkCoord.X >> 4, chunkCoord.Z >> 4);
    }

    // ... other event handlers ...
}
```

### Extract NetworkHandler
```csharp
public class NetworkHandler {
    private readonly LiveMap _server;
    private IServerNetworkChannel? _channel;

    public NetworkHandler(LiveMap server) {
        _server = server;
        RegisterChannel();
    }

    private void RegisterChannel() {
        _channel = _server.Sapi.Network.RegisterChannel(_server.ModId)
            .RegisterMessageType<ColormapPacket>()
            .SetMessageHandler<ColormapPacket>(ReceiveColormap);
    }

    public void SendPacket<T>(T packet, IPlayer? receiver = null) {
        _channel?.SendPacket(packet, receiver as IServerPlayer);
    }

    private void ReceiveColormap(IServerPlayer player, ColormapPacket packet) {
        // ... packet handling logic ...
    }
}
```

### Refactored LiveMap
```csharp
public sealed class LiveMap {
    public static LiveMap Api { get; private set; } = null!;
    public ICoreServerAPI Sapi { get; }
    public string ModId => _mod.Mod.Info.ModID;

    public ConfigManager ConfigManager { get; }
    public Colormap Colormap { get; }
    public SepiaColors SepiaColors { get; }
    public CommandHandler CommandHandler { get; }
    public LayerRegistry LayerRegistry { get; }
    public RendererRegistry RendererRegistry { get; }
    public AsyncTaskManager? AsyncTaskManager { get; private set; }
    public RenderTaskManager? RenderTaskManager { get; private set; }
    public WebServer? WebServer { get; }

    private readonly LiveMapMod _mod;
    private readonly EventCoordinator _eventCoordinator;
    private readonly NetworkHandler _networkHandler;

    public LiveMap(LiveMapMod mod, ICoreServerAPI api) {
        Api = this;
        Sapi = api;
        _mod = mod;

        Files.SavegameIdentifier = Sapi.World.SavegameIdentifier;
        GamePaths.EnsurePathExists(GamePaths.ModConfig);
        GamePaths.EnsurePathExists(Files.DataDir);

        ConfigManager = new ConfigManager(this);
        Files.ExtractWebFiles(this);

        Colormap = new Colormap();
        SepiaColors = new SepiaColors(this);
        CommandHandler = new CommandHandler(this);
        LayerRegistry = [];
        RendererRegistry = [];

        AsyncTaskManager = new AsyncTaskManager(this);
        RenderTaskManager = new RenderTaskManager(this);
        WebServer = new WebServer(this);

        _eventCoordinator = new EventCoordinator(this);
        _networkHandler = new NetworkHandler(this);

        Sapi.Event.RegisterCallback(_ => {
            Colormap.LoadFromDisk(Sapi.World);
            RendererRegistry.RegisterBuiltIns();
            LayerRegistry.RegisterBuiltIns();
        }, 1);
    }

    public void Reload() {
        AsyncTaskManager?.Dispose();
        RenderTaskManager?.Dispose();

        ConfigManager.Reload();
        WebServer?.Reload();

        AsyncTaskManager = new AsyncTaskManager(this);
        RenderTaskManager = new RenderTaskManager(this);
    }

    public void Dispose() {
        _eventCoordinator.Dispose();
        _networkHandler.Dispose();
        ConfigManager.Dispose();
        // ... other disposals ...
    }
}
```

## Benefits
1. **Single Responsibility**: Each class has one job
2. **Easier Testing**: Can test components independently
3. **Better Maintainability**: Changes isolated to specific classes
4. **Clearer Intent**: Class names describe purpose
5. **Reduced Coupling**: Dependencies are explicit

## Testing Recommendations
1. Refactor incrementally, one class at a time
2. Add unit tests for extracted classes
3. Verify functionality after each extraction
4. Use dependency injection for testability

## Related Issues
- See REFACTOR-09: Mixed Concerns in WebServer (similar issue)

## References
- [Single Responsibility Principle](https://en.wikipedia.org/wiki/Single-responsibility_principle)
- [Refactoring: Improving the Design of Existing Code](https://martinfowler.com/books/refactoring.html)
