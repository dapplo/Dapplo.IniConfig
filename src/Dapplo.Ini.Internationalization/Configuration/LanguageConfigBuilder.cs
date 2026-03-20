// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Dapplo.Ini.Internationalization.Interfaces;

namespace Dapplo.Ini.Internationalization.Configuration;

/// <summary>
/// Fluent builder that configures a <see cref="LanguageConfig"/>.
/// </summary>
/// <example>
/// <code>
/// var langConfig = LanguageConfigBuilder.Create("myapp")
///     .WithDirectory("/path/to/lang")
///     .WithBaseLanguage("en-US")
///     .WithCurrentLanguage("de-DE")
///     .AddSection&lt;IMainLanguage&gt;(new MainLanguageImpl())
///     .AddSection&lt;ICoreLanguage&gt;(new CoreLanguageImpl(), "/path/to/core/lang")
///     .UseFallback()
///     .MonitorFiles()
///     .Build();
/// </code>
/// </example>
public sealed class LanguageConfigBuilder
{
    private readonly string _basename;
    private string? _defaultDirectory;
    private string? _baseLanguage;
    private string? _currentLanguage;
    private bool _useFallback;
    private string? _fallbackLanguage;
    private bool _monitorFiles;

    // Registered sections: type → (instance, optional override directory)
    private readonly List<(Type Type, LanguageSectionBase Section, string? Directory)> _sections = new();

    private LanguageConfigBuilder(string basename)
    {
        _basename = basename;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="LanguageConfigBuilder"/> for the application with the given
    /// <paramref name="basename"/>.
    /// </summary>
    /// <param name="basename">
    /// Base name used in the language file naming convention.  For an application named
    /// <c>"myapp"</c> the files would be named <c>myapp.en-US.ini</c>, etc.
    /// </param>
    public static LanguageConfigBuilder Create(string basename)
    {
        if (string.IsNullOrWhiteSpace(basename))
            throw new ArgumentException("Basename must not be empty.", nameof(basename));

        return new LanguageConfigBuilder(basename);
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the default directory that is searched for language pack files.
    /// Individual sections can override this via <see cref="AddSection{T}(T, string)"/>.
    /// </summary>
    public LanguageConfigBuilder WithDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory must not be empty.", nameof(directory));

        _defaultDirectory = directory;
        return this;
    }

    /// <summary>
    /// Sets the base (reference) language.
    /// The base language file is always loaded first so that any key missing from the
    /// active language is displayed in the base language rather than as a
    /// <c>###key###</c> sentinel.
    /// </summary>
    /// <param name="ietf">IETF language tag (e.g. <c>"en-US"</c>).</param>
    public LanguageConfigBuilder WithBaseLanguage(string ietf)
    {
        if (string.IsNullOrWhiteSpace(ietf))
            throw new ArgumentException("Base language tag must not be empty.", nameof(ietf));

        _baseLanguage = ietf;
        return this;
    }

    /// <summary>
    /// Sets the language to activate on first load.
    /// Defaults to the base language when not specified.
    /// </summary>
    /// <param name="ietf">IETF language tag (e.g. <c>"de-DE"</c>).</param>
    public LanguageConfigBuilder WithCurrentLanguage(string ietf)
    {
        if (string.IsNullOrWhiteSpace(ietf))
            throw new ArgumentException("Current language tag must not be empty.", nameof(ietf));

        _currentLanguage = ietf;
        return this;
    }

    /// <summary>
    /// Enables fallback behaviour: when a key is missing from the active language the
    /// framework uses the base language value instead of the <c>###key###</c> sentinel.
    /// The base language file is always loaded first as the fallback layer.
    /// </summary>
    /// <param name="ietf">
    /// Optional IETF tag of a specific fallback language.
    /// When <c>null</c> (or not supplied) the base language is used as the fallback.
    /// </param>
    public LanguageConfigBuilder UseFallback(string? ietf = null)
    {
        _useFallback = true;
        _fallbackLanguage = ietf;
        return this;
    }

    /// <summary>
    /// Enables file-system monitoring. When any language file in a registered directory
    /// is modified, all sections are reloaded and <see cref="LanguageConfig.LanguageChanged"/>
    /// is raised.
    /// </summary>
    public LanguageConfigBuilder MonitorFiles()
    {
        _monitorFiles = true;
        return this;
    }

    /// <summary>
    /// Registers a language section.
    /// The section's <see cref="ILanguageSection.ModuleName"/> determines which file name
    /// pattern is used (with or without module component).
    /// </summary>
    /// <typeparam name="T">The language section interface type.</typeparam>
    /// <param name="section">The generated concrete section instance.</param>
    /// <param name="directory">
    /// Optional override directory for this section's language files.
    /// When <c>null</c> the default directory set by <see cref="WithDirectory"/> is used.
    /// </param>
    public LanguageConfigBuilder AddSection<T>(T section, string? directory = null)
        where T : ILanguageSection
    {
        if (section is null) throw new ArgumentNullException(nameof(section));
        if (section is not LanguageSectionBase)
            throw new ArgumentException(
                $"Section must derive from {nameof(LanguageSectionBase)}.", nameof(section));

        _sections.Add((typeof(T), (LanguageSectionBase)(object)section, directory));
        return this;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and loads a <see cref="LanguageConfig"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="WithBaseLanguage"/> has not been called, or when no default
    /// directory is set and a section has no override directory.
    /// </exception>
    public LanguageConfig Build()
    {
        var config = CreateCore();
        config.Load();
        return config;
    }

    /// <summary>
    /// Asynchronously builds and loads a <see cref="LanguageConfig"/>.
    /// </summary>
    public async Task<LanguageConfig> BuildAsync(CancellationToken cancellationToken = default)
    {
        var config = CreateCore();
        await config.LoadAsync(cancellationToken).ConfigureAwait(false);
        return config;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private LanguageConfig CreateCore()
    {
        if (string.IsNullOrEmpty(_baseLanguage))
            throw new InvalidOperationException(
                "A base language must be specified via WithBaseLanguage().");

        // Build the resolved section list, applying the default directory where needed.
        var resolvedSections =
            new List<(Type Type, LanguageSectionBase Section, string Directory)>(_sections.Count);

        foreach (var (type, section, overrideDir) in _sections)
        {
            var dir = overrideDir ?? _defaultDirectory;
            if (string.IsNullOrEmpty(dir))
                throw new InvalidOperationException(
                    $"No directory specified for section '{type.Name}'. " +
                    "Call WithDirectory() or pass a directory to AddSection().");

            resolvedSections.Add((type, section, dir!));
        }

        // When UseFallback() was called without an explicit language, we pass null to LanguageConfig
        // so it uses the base language as the fallback (which is the default behaviour anyway).
        string? effectiveFallback = _useFallback ? _fallbackLanguage : null;

        return new LanguageConfig(
            _basename,
            _baseLanguage!,
            _currentLanguage ?? _baseLanguage!,
            effectiveFallback,
            _monitorFiles,
            resolvedSections);
    }
}
