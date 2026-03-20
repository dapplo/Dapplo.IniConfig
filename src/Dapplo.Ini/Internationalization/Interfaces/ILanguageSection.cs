// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Internationalization.Interfaces;

/// <summary>
/// Optional marker interface for language sections generated from interfaces annotated with
/// <see cref="Attributes.IniLanguageSectionAttribute"/>.
/// Consumer interfaces are not required to extend this.
/// </summary>
public interface ILanguageSection
{
    /// <summary>
    /// The <c>[SectionName]</c> header that must be present in the language file.
    /// Corresponds to <see cref="Attributes.IniLanguageSectionAttribute.SectionName"/>,
    /// or the interface name with the leading <c>I</c> stripped when no explicit name is given.
    /// </summary>
    string SectionName { get; }

    /// <summary>
    /// Optional module name used in the file naming convention.
    /// Corresponds to <see cref="Attributes.IniLanguageSectionAttribute.ModuleName"/>.
    /// When set, the file is <c>{basename}.{moduleName}.{ietf}.ini</c>;
    /// when <c>null</c>, the file is <c>{basename}.{ietf}.ini</c>.
    /// </summary>
    string? ModuleName { get; }
}



