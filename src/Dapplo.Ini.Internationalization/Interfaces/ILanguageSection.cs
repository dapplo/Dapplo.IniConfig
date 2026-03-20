// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Internationalization.Interfaces;

/// <summary>
/// Marker interface for language sections generated from interfaces annotated with
/// <see cref="Attributes.IniLanguageSectionAttribute"/>.
/// </summary>
public interface ILanguageSection
{
    /// <summary>
    /// Optional module name used to locate the correct language pack file.
    /// Corresponds to <see cref="Attributes.IniLanguageSectionAttribute.ModuleName"/>.
    /// <c>null</c> when no module name was specified.
    /// </summary>
    string? ModuleName { get; }
}
