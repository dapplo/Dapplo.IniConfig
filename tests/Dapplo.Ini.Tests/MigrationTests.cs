// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Dapplo.Ini.Interfaces;

namespace Dapplo.Ini.Tests;

/// <summary>
/// Tests for migration support features:
/// 1. <see cref="IUnknownKey{TSelf}"/> — generic static-virtual pattern.
/// 2. <see cref="IUnknownKey"/>        — non-generic partial-class pattern.
/// 3. <see cref="IniConfigBuilder.OnUnknownKey"/> — builder-level callback.
/// 4. <see cref="IniConfigBuilder.TrackAssemblyVersion"/> — version tracking.
/// </summary>
[Collection("IniConfigRegistry")]
public sealed class MigrationTests : IDisposable
{
    private readonly string _tempDir;

    public MigrationTests()
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

    // ── IUnknownKey<TSelf> (generic, static-virtual) ──────────────────────────

    [Fact]
    public void GenericUnknownKey_RenamesMigratedProperty()
    {
        // File contains the OLD key "OldName" — the section now has "DisplayName".
        WriteIni("migration1.ini", "[Migration]\nOldName = Migrated!\nMaxCount = 5");

        var section = new MigrationSettingsImpl();
        IniConfigRegistry.ForFile("migration1.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IMigrationSettings>(section)
            .Build();

        // The OnUnknownKey bridge should have copied OldName → DisplayName.
        Assert.Equal("Migrated!", section.DisplayName);
        Assert.Equal(5, section.MaxCount);
        Assert.True(section.UnknownKeyCalled);
        Assert.Equal("OldName", section.LastUnknownKey);
    }

    [Fact]
    public void GenericUnknownKey_AfterLoadHookStillFires()
    {
        WriteIni("migration2.ini", "[Migration]\nDisplayName = Test");

        var section = new MigrationSettingsImpl();
        IniConfigRegistry.ForFile("migration2.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IMigrationSettings>(section)
            .Build();

        Assert.True(section.AfterLoadCalled, "IAfterLoad<TSelf> hook should still fire.");
    }

    [Fact]
    public void GenericUnknownKey_KnownKeysDoNotTriggerCallback()
    {
        WriteIni("migration3.ini", "[Migration]\nDisplayName = Known\nMaxCount = 10");

        var section = new MigrationSettingsImpl();
        IniConfigRegistry.ForFile("migration3.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IMigrationSettings>(section)
            .Build();

        // Known keys must NOT trigger the unknown-key callback.
        Assert.False(section.UnknownKeyCalled, "Known keys should not trigger OnUnknownKey.");
    }

    [Fact]
    public void GenericUnknownKey_AlsoFiresDuringReload()
    {
        WriteIni("migration-reload.ini", "[Migration]\nDisplayName = v1");

        var section = new MigrationSettingsImpl();
        var config = IniConfigRegistry.ForFile("migration-reload.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IMigrationSettings>(section)
            .Build();

        // Overwrite the file with an old/renamed key.
        File.WriteAllText(Path.Combine(_tempDir, "migration-reload.ini"),
            "[Migration]\nOldName = ReloadedValue");

        config.Reload();

        Assert.True(section.UnknownKeyCalled);
        Assert.Equal("ReloadedValue", section.DisplayName);
    }

    // ── IUnknownKey (non-generic, partial-class) ──────────────────────────────

    [Fact]
    public void LegacyUnknownKey_RenamesMigratedProperty()
    {
        WriteIni("legacy-migration.ini", "[LegacyMigration]\nOldValue = 42");

        var section = new LegacyMigrationSettingsImpl();
        IniConfigRegistry.ForFile("legacy-migration.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<ILegacyMigrationSettings>(section)
            .Build();

        Assert.Equal(42, section.Value);
        Assert.Equal("OldValue", section.LastUnknownKey);
    }

    [Fact]
    public void LegacyUnknownKey_UnknownValue_IsPassedToHandler()
    {
        WriteIni("legacy-migration2.ini", "[LegacyMigration]\nObsoleteKey = hello");

        var section = new LegacyMigrationSettingsImpl();
        IniConfigRegistry.ForFile("legacy-migration2.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<ILegacyMigrationSettings>(section)
            .Build();

        Assert.Equal("ObsoleteKey", section.LastUnknownKey);
        Assert.Equal("hello", section.LastUnknownValue);
    }

    // ── Builder-level OnUnknownKey callback ───────────────────────────────────

    [Fact]
    public void BuilderCallback_IsInvokedForUnknownKeys()
    {
        WriteIni("builder-callback.ini", "[General]\nUnknownProp = foo\nAppName = Bar");

        var unknownKeys = new List<(string Section, string Key, string? Value)>();

        var section = new GeneralSettingsImpl();
        IniConfigRegistry.ForFile("builder-callback.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .OnUnknownKey((s, k, v) => unknownKeys.Add((s, k, v)))
            .Build();

        // "UnknownProp" is not on IGeneralSettings — callback should fire.
        Assert.Single(unknownKeys);
        Assert.Equal("General", unknownKeys[0].Section);
        Assert.Equal("UnknownProp", unknownKeys[0].Key);
        Assert.Equal("foo", unknownKeys[0].Value);

        // "AppName" IS known — it must NOT appear in the list.
        Assert.DoesNotContain(unknownKeys, t => t.Key == "AppName");
    }

    [Fact]
    public void BuilderCallback_IsInvokedDuringReload()
    {
        WriteIni("builder-reload-callback.ini", "[General]\nAppName = Test");

        var unknownKeys = new List<string>();
        var section = new GeneralSettingsImpl();
        var config = IniConfigRegistry.ForFile("builder-reload-callback.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .OnUnknownKey((_, k, _) => unknownKeys.Add(k))
            .Build();

        Assert.Empty(unknownKeys); // no unknown keys on first load

        File.WriteAllText(Path.Combine(_tempDir, "builder-reload-callback.ini"),
            "[General]\nAppName = New\nRenamedKey = value");
        config.Reload();

        Assert.Contains("RenamedKey", unknownKeys);
    }

    [Fact]
    public void BuilderCallback_ThrowsWhenCallbackIsNull()
    {
        var builder = IniConfigRegistry.ForFile("null-cb.ini");
        Assert.Throws<ArgumentNullException>(() => builder.OnUnknownKey(null!));
    }

    // ── Assembly version tracking ─────────────────────────────────────────────

    [Fact]
    public void TrackAssemblyVersion_WritesVersionToFile()
    {
        var section = new GeneralSettingsImpl();
        var config = IniConfigRegistry.ForFile("version-track.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .TrackAssemblyVersion()
            .Build();

        // Mark dirty and save so that __Version is written.
        section.AppName = "WithVersion";
        config.Save();

        var written = File.ReadAllText(config.LoadedFromPath!);
        Assert.Contains("__Version", written, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrackAssemblyVersion_StoredVersionAccessibleViaGetRawValue()
    {
        // Write a file that already has a __Version entry.
        WriteIni("stored-version.ini", "[General]\nAppName = Test\n__Version = 1.0.0.0");

        string? storedVersionStr = null;
        var section = new GeneralSettingsImpl();

        // Use the builder-level unknown key callback only as a safety net —
        // __Version must NOT appear there.
        bool versionAppearedInUnknownCallback = false;
        IniConfigRegistry.ForFile("stored-version.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .TrackAssemblyVersion()
            .OnUnknownKey((_, k, _) =>
            {
                if (k.Equals("__Version", StringComparison.OrdinalIgnoreCase))
                    versionAppearedInUnknownCallback = true;
            })
            .Build();

        storedVersionStr = section.GetRawValue("__Version");

        Assert.Equal("1.0.0.0", storedVersionStr);
        Assert.False(versionAppearedInUnknownCallback,
            "__Version must not appear in the unknown-key callback.");
    }

    [Fact]
    public void TrackAssemblyVersion_VersionNotWritten_WhenNotOptedIn()
    {
        var section = new GeneralSettingsImpl();
        var config = IniConfigRegistry.ForFile("no-version-track.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IGeneralSettings>(section)
            .Build(); // No TrackAssemblyVersion()

        section.AppName = "NoVersion";
        config.Save();

        var written = File.ReadAllText(config.LoadedFromPath!);
        Assert.DoesNotContain("__Version", written, StringComparison.OrdinalIgnoreCase);
    }

    // ── Integration: version-aware migration via IAfterLoad ───────────────────

    [Fact]
    public void VersionTracking_AfterLoad_CanCompareStoredVersionToCurrentVersion()
    {
        // Simulate a file written by version 0.0.0.1 (very old).
        WriteIni("version-migration.ini",
            "[Migration]\nDisplayName = OldName\n__Version = 0.0.0.1");

        Version? storedVersion = null;
        Version? currentVersion = null;

        // We piggyback on the IAfterLoad pattern rather than building a special interface.
        // Here we verify the raw value is accessible; real code would do the comparison in OnAfterLoad.
        var section = new MigrationSettingsImpl();
        IniConfigRegistry.ForFile("version-migration.ini")
            .AddSearchPath(_tempDir)
            .RegisterSection<IMigrationSettings>(section)
            .TrackAssemblyVersion()
            .Build();

        var raw = section.GetRawValue("__Version");
        Version.TryParse(raw, out storedVersion);
        currentVersion = typeof(IMigrationSettings).Assembly.GetName().Version;

        Assert.NotNull(storedVersion);
        Assert.NotNull(currentVersion);
        // The stored 0.0.0.1 should be older than the actual assembly version (which is at least 0.0.0.0).
        // (We just verify they are both parseable; real comparison would be in OnAfterLoad.)
        Assert.Equal(new Version(0, 0, 0, 1), storedVersion);
    }
}
