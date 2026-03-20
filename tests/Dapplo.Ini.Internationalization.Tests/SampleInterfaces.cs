// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Dapplo.Ini.Internationalization.Attributes;
using Dapplo.Ini.Internationalization.Interfaces;

namespace Dapplo.Ini.Internationalization.Tests;

/// <summary>
/// Base language section.
/// No explicit section name → SectionName derived from interface = "MainLanguage".
/// No ModuleName → file is <c>testapp.{ietf}.ini</c>, reads <c>[MainLanguage]</c> block.
/// Does NOT extend ILanguageSection to verify that is optional.
/// </summary>
[IniLanguageSection]
public interface IMainLanguage
{
    string WelcomeMessage { get; }
    string ErrorTitle { get; }
    string SaveButton { get; }
    string CancelButton { get; }
    string MultiLine { get; }
    string TabValue { get; }
    string BackslashValue { get; }
}

/// <summary>
/// Named section "core" — reads the <c>[core]</c> block from <c>testapp.{ietf}.ini</c> (no module file).
/// Optionally extends ILanguageSection to show it still works when present.
/// </summary>
[IniLanguageSection("core")]
public interface ICoreLanguage : ILanguageSection
{
    string CoreTitle { get; }
    string CoreStatus { get; }
}

/// <summary>
/// Language section whose interface also extends IReadOnlyDictionary so the dynamic
/// indexer can be used.
/// SectionName derived = "DictionaryLanguage", reads <c>[DictionaryLanguage]</c> from main file.
/// </summary>
[IniLanguageSection]
public interface IDictionaryLanguage : IReadOnlyDictionary<string, string>
{
    string WelcomeMessage { get; }
}

/// <summary>
/// Plugin / module language section.
/// SectionName = "core" (explicit), ModuleName = "core" → reads <c>[core]</c> from
/// <c>testapp.core.{ietf}.ini</c>.
/// Demonstrates the separate file selection (<see cref="IniLanguageSectionAttribute.ModuleName"/>)
/// and section routing (<see cref="IniLanguageSectionAttribute.SectionName"/>) concerns.
/// </summary>
[IniLanguageSection("core", ModuleName = "core")]
public interface IPluginLanguage
{
    string CoreTitle { get; }
    string CoreStatus { get; }
}



