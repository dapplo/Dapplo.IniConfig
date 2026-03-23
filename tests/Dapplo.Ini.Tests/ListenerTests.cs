// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Dapplo.Ini;
using Dapplo.Ini.Interfaces;

namespace Dapplo.Ini.Tests;

[Collection("IniConfigRegistry")]
public sealed class ListenerTests : IDisposable
{
    private readonly string _tempDir;

    public ListenerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        IniConfigRegistry.Clear();
    }

    public void Dispose()
    {
        IniConfigRegistry.Clear();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteIni(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    // ── Helper listener ───────────────────────────────────────────────────────

    private sealed class RecordingListener : IIniConfigListener
    {
        public string? LoadedPath { get; private set; }
        public string? NotFoundName { get; private set; }
        public string? SavedPath { get; private set; }
        public string? ReloadedPath { get; private set; }
        public (string Operation, Exception Exception)? ErrorInfo { get; private set; }

        public void OnFileLoaded(string filePath)   => LoadedPath   = filePath;
        public void OnFileNotFound(string fileName) => NotFoundName = fileName;
        public void OnSaved(string filePath)        => SavedPath    = filePath;
        public void OnReloaded(string filePath)     => ReloadedPath = filePath;
        public void OnError(string operation, Exception exception) => ErrorInfo = (operation, exception);
    }

    // ── OnFileLoaded ──────────────────────────────────────────────────────────

    [Fact]
    public void Listener_OnFileLoaded_IsCalledWhenFileExists()
    {
        WriteIni("app.ini", "[General]\nAppName = Test");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        IniConfigRegistry.ForFile("app.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .Build();

        Assert.NotNull(listener.LoadedPath);
        Assert.EndsWith("app.ini", listener.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Null(listener.NotFoundName);
    }

    // ── OnFileNotFound ────────────────────────────────────────────────────────

    [Fact]
    public void Listener_OnFileNotFound_IsCalledWhenFileIsMissing()
    {
        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        IniConfigRegistry.ForFile("missing.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .Build();

        Assert.NotNull(listener.NotFoundName);
        Assert.Equal("missing.ini", listener.NotFoundName);
        Assert.Null(listener.LoadedPath);
    }

    // ── OnSaved ───────────────────────────────────────────────────────────────

    [Fact]
    public void Listener_OnSaved_IsCalledAfterSave()
    {
        WriteIni("save.ini", "[General]\nAppName = Saved");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        var config = IniConfigRegistry.ForFile("save.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .Build();

        config.Save();

        Assert.NotNull(listener.SavedPath);
        Assert.EndsWith("save.ini", listener.SavedPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── OnReloaded ────────────────────────────────────────────────────────────

    [Fact]
    public void Listener_OnReloaded_IsCalledAfterReload()
    {
        WriteIni("reload.ini", "[General]\nAppName = Original");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        var config = IniConfigRegistry.ForFile("reload.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .Build();

        config.Reload();

        Assert.NotNull(listener.ReloadedPath);
        Assert.EndsWith("reload.ini", listener.ReloadedPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── Multiple listeners ────────────────────────────────────────────────────

    [Fact]
    public void MultipleListeners_AllReceiveNotifications()
    {
        WriteIni("multi.ini", "[General]\nAppName = Multi");

        var listener1 = new RecordingListener();
        var listener2 = new RecordingListener();
        var section = new GeneralSettingsImpl();

        IniConfigRegistry.ForFile("multi.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener1)
            .AddListener(listener2)
            .Build();

        Assert.NotNull(listener1.LoadedPath);
        Assert.NotNull(listener2.LoadedPath);
        Assert.Equal(listener1.LoadedPath, listener2.LoadedPath);
    }

    // ── No overhead when no listener registered (smoke-test) ──────────────────

    [Fact]
    public void NoListener_DoesNotThrow()
    {
        WriteIni("nolistener.ini", "[General]\nAppName = Test");
        var section = new GeneralSettingsImpl();

        // Should complete without error when no listener is registered
        var config = IniConfigRegistry.ForFile("nolistener.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .Build();

        config.Save();
        config.Reload();
    }

    // ── Async paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Listener_OnFileLoaded_IsCalledFromLoadAsync()
    {
        WriteIni("async.ini", "[General]\nAppName = Async");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        await IniConfigRegistry.ForFile("async.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .BuildAsync();

        Assert.NotNull(listener.LoadedPath);
        Assert.EndsWith("async.ini", listener.LoadedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Listener_OnFileNotFound_IsCalledFromLoadAsync()
    {
        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        await IniConfigRegistry.ForFile("asyncmissing.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .BuildAsync();

        Assert.Equal("asyncmissing.ini", listener.NotFoundName);
        Assert.Null(listener.LoadedPath);
    }

    [Fact]
    public async Task Listener_OnSaved_IsCalledFromSaveAsync()
    {
        WriteIni("saveasync.ini", "[General]\nAppName = SaveAsync");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        var config = await IniConfigRegistry.ForFile("saveasync.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .BuildAsync();

        await config.SaveAsync();

        Assert.NotNull(listener.SavedPath);
        Assert.EndsWith("saveasync.ini", listener.SavedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Listener_OnReloaded_IsCalledFromReloadAsync()
    {
        WriteIni("reloadasync.ini", "[General]\nAppName = ReloadAsync");

        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        var config = await IniConfigRegistry.ForFile("reloadasync.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .BuildAsync();

        await config.ReloadAsync();

        Assert.NotNull(listener.ReloadedPath);
        Assert.EndsWith("reloadasync.ini", listener.ReloadedPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── OnError ───────────────────────────────────────────────────────────────

    [Fact]
    public void Listener_OnError_IsCalledOnSaveError()
    {
        var listener = new RecordingListener();
        var section = new GeneralSettingsImpl();

        // Point the writable path at a directory that doesn't exist so that Save() fails.
        var nonExistentDir = Path.Combine(_tempDir, "nonexistent_dir");
        var writablePath = Path.Combine(nonExistentDir, "saveerror.ini");

        var config = IniConfigRegistry.ForFile("saveerror.ini")
            .AddSearchPath(_tempDir)
            .SetWritablePath(writablePath)
            .RegisterSection<IGeneralSettings>(section)
            .AddListener(listener)
            .Build();

        Assert.ThrowsAny<Exception>(() => config.Save());
        Assert.NotNull(listener.ErrorInfo);
        Assert.Equal("Save", listener.ErrorInfo!.Value.Operation);
    }
}
