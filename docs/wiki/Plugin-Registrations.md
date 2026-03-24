# Plugin / Distributed Registrations

Plugin-based applications face a challenge: plugins are loaded *after* the host has
already called `Build()`, so they cannot call the builder to register their own INI
sections.  `Dapplo.Ini` solves this with a **three-phase Create / AddSection / Load**
pattern that reads all INI files exactly once, after every section — host and plugin —
has been registered.

---

## The three-phase pattern

### Phase 1 — host creates the config (no I/O)

Instead of calling `Build()`, the host calls `Create()`.  This constructs the
`IniConfig`, seeds it with the host's own sections, and registers it in the global
`IniConfigRegistry` — all without touching the file system.

```csharp
// Host startup — create, don't load yet
var config = IniConfigRegistry.ForFile("app.ini")
    .AddSearchPath(AppContext.BaseDirectory)
    .RegisterSection<IHostSettings>(new HostSettingsImpl())
    .Create();
```

At this point:
- The `config` reference can be passed directly to plugins — no guessing of file names.
- **No file has been read yet** — section properties still hold their compiled defaults.

### Phase 2 — plugins add their sections (no I/O)

The host **provides the `IniConfig`** to each plugin.  Each plugin calls
`AddSection<T>()` on the config it receives:

```csharp
// Inside a plugin pre-init method — config is provided by the host
public void PreInit(IniConfig config)
{
    config.AddSection<IPluginSettings>(new PluginSettingsImpl());
}
```

`AddSection<T>()` is pure in-memory — it does not read or write any file.

### Phase 3 — host loads everything at once (single file read)

After all plugins have registered their sections, the host calls `Load()` (or
`LoadAsync()`).  The full [[Loading-Life-Cycle]] is applied once for every registered
section:

```csharp
// Phase 3 — single load reads all files for every section
config.Load();

// Or the async equivalent
await config.LoadAsync(cancellationToken);
```

After `Load()` returns, all sections — host and plugin alike — have their values
populated from the INI file.

---

## Full example

```csharp
// ── Program.cs (host) ─────────────────────────────────────────────────────────

// Phase 1: create (no I/O)
var config = IniConfigRegistry.ForFile("app.ini")
    .AddSearchPath(AppContext.BaseDirectory)
    .AddDefaultsFile("/etc/myapp/defaults.ini")
    .RegisterSection<IHostSettings>(new HostSettingsImpl())
    .Create();

// Phase 2: load plugins, pass the config so each plugin can register its section
foreach (var plugin in PluginLoader.LoadAll())
    plugin.PreInit(config);

// Phase 3: load (single file read for every section)
config.Load();

// All sections are now populated
var hostSettings   = config.GetSection<IHostSettings>();
var pluginSettings = config.GetSection<IPluginSettings>();
```

```csharp
// ── PluginA.cs ────────────────────────────────────────────────────────────────

public class PluginA
{
    public void PreInit(IniConfig config)
    {
        // The host provides the config — no need to know or guess the file name
        config.AddSection<IPluginASettings>(new PluginASettingsImpl());
    }
}
```

### Alternative: registry-based lookup

When passing `IniConfig` directly is impractical (e.g. a plugin that is initialised
through a third-party plugin host), the plugin can retrieve the config from the global
registry, provided the host has already called `Create()`:

```csharp
// Plugin retrieves the config from the registry when it cannot be injected
IniConfigRegistry.Get("app.ini").AddSection<IPluginASettings>(new PluginASettingsImpl());
```

Or use the `IniConfigRegistry` convenience overload:

```csharp
IniConfigRegistry.AddSection<IPluginASettings>("app.ini", new PluginASettingsImpl());
```

---

## Using `Build()` vs `Create()`

| Method | I/O on call | When to use |
|--------|-------------|-------------|
| `Build()` | Immediate | Simple apps — no plugins that need to register sections before loading |
| `Create()` + `Load()` | Deferred | Plugin-based apps — all sections must be registered before the single load |

`Build()` is equivalent to calling `Create()` and then immediately calling `Load()` on
the returned config.  Existing code that uses `Build()` continues to work unchanged.

---

## Async variant

```csharp
// Phase 1
var config = IniConfigRegistry.ForFile("app.ini")
    .AddSearchPath(AppContext.BaseDirectory)
    .RegisterSection<IHostSettings>(new HostSettingsImpl())
    .Create();

// Phase 2 — plugins add their sections (synchronous, no I/O)
foreach (var plugin in PluginLoader.LoadAll())
    plugin.PreInit();

// Phase 3 — async load (applies IValueSourceAsync and IAfterLoadAsync)
await config.LoadAsync(cancellationToken);
```

---

## DI integration with deferred loading

`Create()` + `LoadAsync()` pairs naturally with the DI fire-and-forget pattern.
Sections and the `IniConfig` are added to the DI container immediately after `Create()`;
`LoadAsync()` runs in the background and consumers await `InitialLoadTask` before
reading values:

```csharp
// Phase 1 — create: config + section references are stable and injectable
var hostSection   = new HostSettingsImpl();
var pluginSection = new PluginSettingsImpl();

var config = IniConfigRegistry.ForFile("app.ini")
    .AddSearchPath(AppContext.BaseDirectory)
    .RegisterSection<IHostSettings>(hostSection)
    .Create();

config.AddSection<IPluginSettings>(pluginSection);

// Register as DI singletons before loading completes
builder.Services.AddSingleton<IHostSettings>(hostSection);
builder.Services.AddSingleton<IPluginSettings>(pluginSection);
builder.Services.AddSingleton(config);

// Phase 3 — fire-and-forget async load
var loadTask = config.LoadAsync(cancellationToken);
```

```csharp
// Consumer — await loading before reading values
public class MyWorker
{
    private readonly IHostSettings _settings;
    private readonly IniConfig     _config;

    public MyWorker(IHostSettings settings, IniConfig config)
    {
        _settings = settings;
        _config   = config;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _config.InitialLoadTask;          // wait for the load to finish
        Console.WriteLine(_settings.AppName);  // safe to read now
    }
}
```

> **Note:** `InitialLoadTask` is only non-trivial when `BuildAsync()` is used.  When you
> call `LoadAsync()` directly, store the returned `Task` yourself if you need to await it.

---

## API summary

### `IniConfigBuilder`

| Method | Description |
|--------|-------------|
| `Create()` | Creates and registers the `IniConfig` without loading any files. Returns the `IniConfig` for the pre-load phase. |

### `IniConfig`

| Method | Description |
|--------|-------------|
| `AddSection<T>(section)` | Registers `section` under the interface type `T`; no file I/O. Returns `section` for chaining. |
| `AddSection(section)` | Non-generic overload; infers the interface type by reflection (AOT-unfriendly — prefer the generic overload). |
| `Load()` | Applies the full [[Loading-Life-Cycle]] once for all registered sections. Returns `this` for chaining. |
| `LoadAsync(ct)` | Async variant of `Load()`; also applies `IValueSourceAsync` sources and calls `IAfterLoadAsync` hooks. |

### `IniConfigRegistry`

| Method | Description |
|--------|-------------|
| `AddSection<T>(fileName, section)` | Convenience overload: `IniConfigRegistry.Get(fileName).AddSection<T>(section)` |

---

## See also

- [[Loading-Configuration]] — `IniConfigBuilder` fluent API, `Create()` and `Build()`
- [[Loading-Life-Cycle]] — exact resolution order applied by `Load()`
- [[Registry-API]] — complete API reference
- [[Singleton-and-DI]] — using the config and its sections as DI singletons
- [[Async-Support]] — `BuildAsync`, `LoadAsync`, `InitialLoadTask`, and async value sources
