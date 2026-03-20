// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Internationalization.Attributes;

/// <summary>
/// Marks an interface as a language section. The source generator creates a concrete
/// implementation where each <c>string</c> property returns the translated value
/// (or <c>###PropertyName###</c> when the key is missing from the loaded language file).
/// </summary>
/// <remarks>
/// <para>
/// Every property in a language <c>.ini</c> file <strong>must</strong> appear inside a
/// <c>[SectionName]</c> block. Keys outside any section header are silently ignored.
/// </para>
/// <para>
/// The two attribute properties govern file selection and section routing independently:
/// </para>
/// <list type="bullet">
///   <item>
///     <term><see cref="SectionName"/></term>
///     <description>
///     The <c>[SectionName]</c> header to read from the file.
///     When not provided the generator derives it from the interface name
///     by stripping the leading <c>I</c> (e.g. <c>IMainLanguage</c> → <c>MainLanguage</c>).
///     </description>
///   </item>
///   <item>
///     <term><see cref="ModuleName"/></term>
///     <description>
///     Optional. When set, the loader reads from <c>{basename}.{moduleName}.{ietf}.ini</c>.
///     When omitted, the loader reads from <c>{basename}.{ietf}.ini</c>.
///     </description>
///   </item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class IniLanguageSectionAttribute : Attribute
{
    /// <summary>
    /// The <c>[SectionName]</c> header that must be present in the language file.
    /// When <c>null</c> the generator derives the section name from the interface name
    /// by stripping the leading <c>I</c> prefix (e.g. <c>IMainLanguage</c> → <c>MainLanguage</c>).
    /// </summary>
    public string? SectionName { get; }

    /// <summary>
    /// Optional module name used in the language file naming convention.
    /// <list type="bullet">
    ///   <item>When set: <c>{basename}.{moduleName}.{ietf}.ini</c></item>
    ///   <item>When <c>null</c>: <c>{basename}.{ietf}.ini</c></item>
    /// </list>
    /// </summary>
    public string? ModuleName { get; set; }

    /// <summary>
    /// Initialises a new instance of <see cref="IniLanguageSectionAttribute"/>.
    /// </summary>
    /// <param name="sectionName">
    /// The <c>[SectionName]</c> header in the language file.
    /// When <c>null</c> the section name is derived from the interface name.
    /// </param>
    public IniLanguageSectionAttribute(string? sectionName = null)
    {
        SectionName = sectionName;
    }
}


