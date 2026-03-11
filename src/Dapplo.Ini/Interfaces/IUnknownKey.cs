// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Interfaces;

/// <summary>
/// A callback invoked when a key is read from an INI file that does not correspond to any
/// declared property on the registered section interface.
/// This is useful for migration scenarios where a key was renamed or removed.
/// </summary>
/// <param name="sectionName">The INI section name containing the unknown key.</param>
/// <param name="key">The key found in the file that has no matching property.</param>
/// <param name="value">The raw string value associated with the unknown key.</param>
public delegate void UnknownKeyCallback(string sectionName, string key, string? value);

/// <summary>
/// Dispatch interface used by the framework to pass unknown keys to a section instance.
/// Consumer code should prefer the generic <see cref="IUnknownKey{TSelf}"/> overload
/// which allows the implementation to live directly in the section interface via static virtuals.
/// Implement this on your section class (e.g. in a partial class) to handle unknown/obsolete keys.
/// </summary>
/// <remarks>
/// A key is considered "unknown" when it is present in the INI file but does not match any
/// declared property on the registered section interface.
/// This can happen when:
/// <list type="bullet">
///   <item>A property was renamed — the old key name remains in existing user files.</item>
///   <item>A property was removed — the obsolete key persists in existing files.</item>
/// </list>
/// Unknown keys can be used to implement migration logic:
/// read the old value via the <paramref name="value"/> argument and map it to the
/// new or replacement property.
/// </remarks>
/// <example>
/// Rename migration using the non-generic (partial-class) pattern:
/// <code>
/// // In your partial class (e.g. AppSettingsPartial.cs):
/// public partial class AppSettingsImpl : IUnknownKey
/// {
///     public void OnUnknownKey(string key, string? value)
///     {
///         // "OldTimeout" was renamed to "Timeout"
///         if (key.Equals("OldTimeout", StringComparison.OrdinalIgnoreCase))
///             Timeout = int.TryParse(value, out var t) ? t : 30;
///     }
/// }
/// </code>
/// </example>
public interface IUnknownKey
{
    /// <summary>
    /// Called when a key found in the INI file has no matching property on this section.
    /// </summary>
    /// <param name="key">The unrecognised key name.</param>
    /// <param name="value">The raw string value for that key.</param>
    void OnUnknownKey(string key, string? value);
}

#if NET7_0_OR_GREATER
/// <summary>
/// Lifecycle interface invoked for every key in the INI file that does not correspond to a
/// declared property on the section interface.
/// Implement this interface on your section interface and provide the hook logic as a
/// <c>static</c> override of <see cref="OnUnknownKey"/> — no separate partial class is needed.
/// </summary>
/// <typeparam name="TSelf">The section interface itself (CRTP / curiously-recurring template pattern).</typeparam>
/// <remarks>
/// A key is considered "unknown" when it is present in the INI file but does not match any
/// declared property on the registered section interface.
/// The most common use case is renaming a property: the old key name remains in existing user
/// files and is surfaced here so you can copy the value to the new property.
/// </remarks>
/// <example>
/// <code>
/// [IniSection]
/// public interface IAppSettings : IIniSection, IUnknownKey&lt;IAppSettings&gt;
/// {
///     // New name for what used to be "OldTimeout"
///     [IniValue(DefaultValue = "30")]
///     int Timeout { get; set; }
///
///     static void OnUnknownKey(IAppSettings self, string key, string? value)
///     {
///         // Migrate renamed key
///         if (key.Equals("OldTimeout", StringComparison.OrdinalIgnoreCase))
///             self.Timeout = int.TryParse(value, out var t) ? t : 30;
///     }
/// }
/// </code>
/// </example>
public interface IUnknownKey<TSelf> where TSelf : IUnknownKey<TSelf>
{
    /// <summary>
    /// Called when a key found in the INI file has no matching property on this section.
    /// Override this static method in your section interface to handle renamed or obsolete keys.
    /// The source generator emits a bridge that connects this static virtual method to the
    /// <see cref="IUnknownKey"/> dispatch interface used by the framework.
    /// </summary>
    /// <param name="self">The section instance being populated.</param>
    /// <param name="key">The unrecognised key name.</param>
    /// <param name="value">The raw string value for that key.</param>
    static virtual void OnUnknownKey(TSelf self, string key, string? value) { }
}
#endif
