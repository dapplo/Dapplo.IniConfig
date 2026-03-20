// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Dapplo.Ini.Internationalization.Attributes;
using Dapplo.Ini.Internationalization.Interfaces;

namespace Dapplo.Ini.Internationalization.Tests;

/// <summary>
/// Base language section (no module name → file: testapp.{ietf}.ini).
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
/// Module-specific language section (module = "core" → file: testapp.core.{ietf}.ini).
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
/// indexer can be used. Does NOT extend ILanguageSection.
/// </summary>
[IniLanguageSection]
public interface IDictionaryLanguage : IReadOnlyDictionary<string, string>
{
    string WelcomeMessage { get; }
}

/// <summary>
/// Simulates a plugin-provided language section used in deferred-loading tests.
/// </summary>
[IniLanguageSection("core")]
public interface IPluginLanguage
{
    string CoreTitle { get; }
    string CoreStatus { get; }
}

