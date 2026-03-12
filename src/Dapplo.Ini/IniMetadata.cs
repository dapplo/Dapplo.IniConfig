// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini;

/// <summary>
/// Holds the metadata that was read from the <c>[__metadata__]</c> section of an INI file,
/// or <c>null</c> values when the section was absent on the last load.
/// <para>
/// Access this via <see cref="Configuration.IniConfig.Metadata"/> inside an
/// <see cref="Interfaces.IAfterLoad"/> hook to make version-gated migration decisions.
/// </para>
/// </summary>
/// <example>
/// <code>
/// static void OnAfterLoad(IMySettings self)
/// {
///     var meta    = IniConfigRegistry.Get("myapp.ini").Metadata;
///     var stored  = meta?.Version is not null ? new Version(meta.Version) : new Version(0, 0);
///     var current = typeof(IMySettings).Assembly.GetName().Version!;
///     if (stored &lt; current)
///     {
///         // perform upgrade steps for the current version
///     }
/// }
/// </code>
/// </example>
public sealed class IniMetadata
{
    /// <summary>
    /// The version string read from the <c>Version</c> key in <c>[__metadata__]</c>,
    /// or <c>null</c> when the section / key was not present in the file.
    /// </summary>
    public string? Version { get; internal set; }

    /// <summary>
    /// The application name read from the <c>CreatedBy</c> key in <c>[__metadata__]</c>,
    /// or <c>null</c> when not present.
    /// </summary>
    public string? ApplicationName { get; internal set; }

    /// <summary>
    /// The timestamp string read from the <c>SavedOn</c> key in <c>[__metadata__]</c>,
    /// or <c>null</c> when not present.
    /// This is a human-readable, locale-formatted string intended for informational display only.
    /// </summary>
    public string? SavedOn { get; internal set; }
}

/// <summary>
/// Carries the consumer-configured values for the <c>[__metadata__]</c> section.
/// This is an internal transfer object from <see cref="Configuration.IniConfigBuilder"/> to
/// <see cref="Configuration.IniConfig"/>; it is not part of the public API.
/// </summary>
internal sealed class IniMetadataConfig
{
    public string? Version { get; set; }
    public string? ApplicationName { get; set; }
}
