# Changelog

All notable changes to **Dapplo.Ini** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- Migration support: `IUnknownKey<TSelf>` interface, `OnUnknownKey` callback, `TrackAssemblyVersion`, and optional `[__metadata__]` section (`EnableMetadata`) for version-gated upgrades.
- Async support: `BuildAsync`, `ReloadAsync`, `SaveAsync`, `IAfterLoadAsync`, `IBeforeSaveAsync`, `IAfterSaveAsync`, and `IValueSourceAsync` for remote configuration sources (REST APIs, etc.).
- `InitialLoadTask` property on `IniConfig` for DI-friendly async loading — sections are available as singletons immediately while the load completes in the background.
- Plugin / distributed registrations: three-phase `Create()` + `AddSection<T>()` + `Load()` pattern so plugins can register sections before the single file read.
- Built-in collection converters: `ListConverter<T>`, `ArrayConverter<T>`, and `DictionaryConverter<TKey,TValue>`; the `ValueConverterRegistry` creates them automatically for `List<T>`, `IList<T>`, `T[]`, and `Dictionary<TKey,TValue>`.
- Source generator (`Dapplo.Ini.Generator`) detects `IAfterLoad`, `IBeforeSave`, `IAfterSave`, and `IDataValidation` marker interfaces on section interfaces and emits bridge implementations automatically.
- `IniConfigBuilder.EnableMetadata(version?, applicationName?)` prepends a `[__metadata__]` section (Version, CreatedBy, SavedOn) to saved files; `IniConfig.Metadata` exposes the last-read metadata.
- Targets both `net48` and `net10.0`.

### Changed
- Project renamed from `Dapplo.IniConfig` / `Dapplo.Ini.Config` to **`Dapplo.Ini`**; all namespaces updated accordingly.
- `IniConfig`, `IniConfigRegistry`, and `IniConfigBuilder` moved to the `Dapplo.Ini` namespace; `IniSectionBase` remains in `Dapplo.Ini.Configuration`.

---

## [1.0.0-beta] — Initial beta release

### Added
- Define configuration sections as annotated interfaces (`[IniSection]`, `[IniValue]`).
- Roslyn source generator creates concrete `*Impl` classes automatically — no boilerplate.
- Layered loading: defaults file → user file → admin constants file → external value sources (`IValueSource`).
- In-place reload with singleton guarantee — section object references stay valid after `Reload()`.
- File locking (`LockFile()`) to prevent external modification while the application runs.
- File-change monitoring (`MonitorFile()`) via `FileSystemWatcher` with optional `ReloadDecision` callback to postpone or skip reloads.
- `INotifyDataErrorInfo` validation through `IDataValidation<TSelf>`.
- Transactional updates via `ITransactional` — `Begin()`, `Commit()`, `Rollback()`.
- `INotifyPropertyChanged` / `INotifyPropertyChanging` baked into every generated section.
- Lifecycle hooks (`IAfterLoad`, `IBeforeSave`, `IAfterSave`) implementable directly in the section interface using C# 11 static virtuals.
- Extensible value converter system (`IValueConverter`, `ValueConverterRegistry`).
- `IniConfigRegistry` — thread-safe global registry mapping file names to loaded configurations.
- `AddAppDataPath(applicationName)` helper that resolves `%APPDATA%\<app>` (Linux: `~/.config/<app>`) and creates the directory if absent.

[Unreleased]: https://github.com/dapplo/Dapplo.Ini/compare/v1.0.0-beta...HEAD
[1.0.0-beta]: https://github.com/dapplo/Dapplo.Ini/releases/tag/v1.0.0-beta
