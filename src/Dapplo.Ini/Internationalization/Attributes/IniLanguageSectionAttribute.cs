// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Internationalization.Attributes;

/// <summary>
/// Marks an interface as a language section. The source generator creates a concrete
/// implementation where each <c>string</c> property returns the translated value
/// (or <c>###PropertyName###</c> when the key is missing from the loaded language file).
/// </summary>
/// <remarks>
/// File naming convention:
/// <list type="bullet">
///   <item><c>{basename}.{ietf}.ini</c> — for interfaces without a module name.</item>
///   <item><c>{basename}.{moduleName}.{ietf}.ini</c> — for interfaces with a module name.</item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class IniLanguageSectionAttribute : Attribute
{
    /// <summary>
    /// Optional module name used in the language file naming convention:
    /// <c>{basename}.{moduleName}.{ietf}.ini</c>.
    /// When <c>null</c> the file pattern <c>{basename}.{ietf}.ini</c> is used.
    /// </summary>
    public string? ModuleName { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="IniLanguageSectionAttribute"/>.
    /// </summary>
    /// <param name="moduleName">Optional module name for the language file naming convention.</param>
    public IniLanguageSectionAttribute(string? moduleName = null)
    {
        ModuleName = moduleName;
    }
}
