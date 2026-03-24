// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Attributes;

/// <summary>
/// Provides extra INI-specific metadata for a property on an INI section interface.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class IniValueAttribute : Attribute
{
    /// <summary>
    /// The key name used in the INI file. Defaults to the property name.
    /// </summary>
    public string? KeyName { get; set; }

    /// <summary>
    /// The default value expressed as a string (will be converted via the registered converter).
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// An optional description / comment written above the key in the INI file.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When <c>true</c> the value is never written to the ini file.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// When <c>true</c> the property participates in transactions (old value returned until <c>Commit</c>).
    /// Requires the containing section interface to also implement <see cref="Interfaces.ITransactional"/>.
    /// </summary>
    public bool Transactional { get; set; }

    /// <summary>
    /// When <c>true</c> setting this property raises <c>INotifyPropertyChanging</c> and
    /// <c>INotifyPropertyChanged</c> events from the generated class.
    /// </summary>
    public bool NotifyPropertyChanged { get; set; }

    /// <summary>
    /// When <c>true</c> the property is never loaded from or saved to the INI file.
    /// The property participates in <see cref="IIniSection.ResetToDefaults"/> (default value
    /// is applied) but its value lives only in memory for the lifetime of the application.
    /// Use this for settings that must be configurable at runtime but must not be persisted.
    /// </summary>
    public bool RuntimeOnly { get; set; }
}
