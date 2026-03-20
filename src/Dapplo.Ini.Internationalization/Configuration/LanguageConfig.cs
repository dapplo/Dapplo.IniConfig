// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Globalization;
using System.Text;
using Dapplo.Ini.Internationalization.Interfaces;

namespace Dapplo.Ini.Internationalization.Configuration;

/// <summary>
/// Manages one or more language sections loaded from <c>.ini</c> language packs.
/// </summary>
/// <remarks>
/// Language files follow the naming convention:
/// <list type="bullet">
///   <item><c>{basename}.{ietf}.ini</c> — for sections without a module name.</item>
///   <item><c>{basename}.{moduleName}.{ietf}.ini</c> — for sections with a module name.</item>
/// </list>
/// Values support escape sequences: <c>\n</c> → newline, <c>\t</c> → tab, <c>\\</c> → backslash.
/// Keys are normalized: trimmed, underscores and dashes removed, lowercased.
/// </remarks>
public sealed class LanguageConfig : IDisposable
{
    private readonly string _basename;
    private readonly string _baseLanguage;
    private string _currentLanguage;
    private readonly string? _fallbackLanguage;   // null = use _baseLanguage as fallback

    // Maps section type → (section instance, directory for its language files)
    private readonly Dictionary<Type, (LanguageSectionBase Section, string Directory)> _sections = new();

    // File watchers keyed by directory
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _monitorFiles;

    // Debounce timer to coalesce rapid change events
    private System.Threading.Timer? _debounceTimer;
    private const int DebounceMs = 200;

    private volatile bool _disposed;

    /// <summary>
    /// Raised after the language is reloaded (either via <see cref="SetLanguage"/> or a file-change
    /// notification when monitoring is enabled).
    /// </summary>
    public event EventHandler? LanguageChanged;

    // ── Constructor (internal — use LanguageConfigBuilder) ────────────────────

    internal LanguageConfig(
        string basename,
        string baseLanguage,
        string currentLanguage,
        string? fallbackLanguage,
        bool monitorFiles,
        IEnumerable<(Type Type, LanguageSectionBase Section, string Directory)> sections)
    {
        _basename = basename;
        _baseLanguage = baseLanguage;
        _currentLanguage = currentLanguage;
        _fallbackLanguage = fallbackLanguage;
        _monitorFiles = monitorFiles;

        foreach (var (type, section, dir) in sections)
            _sections[type] = (section, dir);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>The IETF language tag that is currently active.</summary>
    public string CurrentLanguage => _currentLanguage;

    /// <summary>The base (reference) language specified at build time.</summary>
    public string BaseLanguage => _baseLanguage;

    /// <summary>
    /// Returns the language section registered under <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the type has not been registered.</exception>
    public T GetSection<T>() where T : ILanguageSection
    {
        if (_sections.TryGetValue(typeof(T), out var entry))
            return (T)(object)entry.Section;

        throw new InvalidOperationException(
            $"Language section '{typeof(T).Name}' has not been registered.");
    }

    /// <summary>
    /// Loads all language sections using the current language.
    /// Automatically called by <see cref="LanguageConfigBuilder.Build"/>.
    /// </summary>
    public void Load()
    {
        LoadLanguage(_currentLanguage);

        if (_monitorFiles)
            StartMonitoring();
    }

    /// <summary>
    /// Asynchronously loads all language sections using the current language.
    /// Automatically called by <see cref="LanguageConfigBuilder.BuildAsync"/>.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await LoadLanguageAsync(_currentLanguage, cancellationToken).ConfigureAwait(false);

        if (_monitorFiles)
            StartMonitoring();
    }

    /// <summary>
    /// Switches to a new language and reloads all language sections.
    /// </summary>
    /// <param name="ietf">IETF language tag (e.g. <c>"de-DE"</c>, <c>"fr"</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ietf"/> is null or empty.</exception>
    public void SetLanguage(string ietf)
    {
        if (string.IsNullOrWhiteSpace(ietf))
            throw new ArgumentException("Language tag must not be empty.", nameof(ietf));

        _currentLanguage = ietf;
        LoadLanguage(ietf);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Asynchronously switches to a new language and reloads all language sections.
    /// </summary>
    /// <param name="ietf">IETF language tag.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task SetLanguageAsync(string ietf, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ietf))
            throw new ArgumentException("Language tag must not be empty.", nameof(ietf));

        _currentLanguage = ietf;
        await LoadLanguageAsync(ietf, cancellationToken).ConfigureAwait(false);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns a list of available languages by scanning the language file directories.
    /// Each entry contains the IETF language tag and the native name of the language
    /// according to <see cref="CultureInfo.NativeName"/>.
    /// </summary>
    /// <returns>
    /// Distinct IETF tags (as <c>(Ietf, NativeName)</c> tuples) discovered from any of the
    /// registered section directories.  Languages that cannot be parsed as a valid
    /// <see cref="CultureInfo"/> are silently skipped.
    /// </returns>
    public IReadOnlyList<(string Ietf, string NativeName)> GetAvailableLanguages()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string Ietf, string NativeName)>();

        foreach (var entry in _sections.Values)
        {
            var dir = entry.Directory;
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.ini"))
            {
                var ietf = ExtractIetfFromFileName(file);
                if (ietf == null || !seen.Add(ietf)) continue;

                if (!TryGetCultureInfo(ietf, out var ci)) continue;

                result.Add((ietf, ci!.NativeName));
            }
        }

        return result;
    }

    // ── Language loading ──────────────────────────────────────────────────────

    private void LoadLanguage(string language)
    {
        var fallback = _fallbackLanguage ?? _baseLanguage;

        foreach (var kvp in _sections)
        {
            var section = kvp.Value.Section;
            var dir = kvp.Value.Directory;

            section.ClearTranslations();

            // 1. Load fallback/base language first — ensures missing keys fall back gracefully
            LoadLanguageFileIntoSection(section, dir, fallback);

            if (!string.Equals(language, fallback, StringComparison.OrdinalIgnoreCase))
            {
                // 2. Progressive fallback: try the parent culture (e.g. "fr" before "fr-FR")
                var hyphen = language.IndexOf('-');
                if (hyphen > 0)
                    LoadLanguageFileIntoSection(section, dir, language.Substring(0, hyphen));

                // 3. Load the most-specific language file (overrides all previous)
                LoadLanguageFileIntoSection(section, dir, language);
            }
        }
    }

    private async Task LoadLanguageAsync(string language, CancellationToken cancellationToken)
    {
        var fallback = _fallbackLanguage ?? _baseLanguage;

        foreach (var kvp in _sections)
        {
            var section = kvp.Value.Section;
            var dir = kvp.Value.Directory;

            section.ClearTranslations();

            await LoadLanguageFileIntoSectionAsync(section, dir, fallback, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(language, fallback, StringComparison.OrdinalIgnoreCase))
            {
                var hyphen = language.IndexOf('-');
                if (hyphen > 0)
                    await LoadLanguageFileIntoSectionAsync(section, dir, language.Substring(0, hyphen), cancellationToken).ConfigureAwait(false);

                await LoadLanguageFileIntoSectionAsync(section, dir, language, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void LoadLanguageFileIntoSection(LanguageSectionBase section, string directory, string ietf)
    {
        var filePath = ResolveLanguageFilePath(directory, section.ModuleName, ietf);
        if (filePath == null) return;

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        ParseAndApply(section, content);
    }

    private async Task LoadLanguageFileIntoSectionAsync(
        LanguageSectionBase section, string directory, string ietf, CancellationToken cancellationToken)
    {
        var filePath = ResolveLanguageFilePath(directory, section.ModuleName, ietf);
        if (filePath == null) return;

        string content;
#if NET
        content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
#else
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        content = await reader.ReadToEndAsync().ConfigureAwait(false);
#endif
        ParseAndApply(section, content);
    }

    private string? ResolveLanguageFilePath(string directory, string? moduleName, string ietf)
    {
        var fileName = string.IsNullOrEmpty(moduleName)
            ? $"{_basename}.{ietf}.ini"
            : $"{_basename}.{moduleName}.{ietf}.ini";

        var path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Parses <paramref name="content"/> as a flat key=value language file and applies
    /// each entry to <paramref name="section"/>.
    /// Sections within the file (e.g. <c>[SectionName]</c>) are ignored for key routing —
    /// all entries are loaded regardless of which section they appear in.
    /// </summary>
    private static void ParseAndApply(LanguageSectionBase section, string content)
    {
        var span = content.AsSpan();
        while (!span.IsEmpty)
        {
            var line = ReadLine(ref span).Trim();
            if (line.IsEmpty) continue;

            var first = line[0];
            // Skip comments and section headers — all entries go into a flat dictionary
            if (first == ';' || first == '#' || first == '[') continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            // Key: trim, remove underscores and dashes, lowercase
            var rawKey = line.Slice(0, eq).TrimEnd().ToString();
            var normalizedKey = LanguageSectionBase.NormalizeKey(rawKey);

            // Value: everything after the first '=' is taken as-is (no leading trim — trimming
            // was intentionally excluded per spec to preserve leading spaces in translations)
            var rawValue = line.Slice(eq + 1).ToString();
            var value = UnescapeValue(rawValue);

            section.SetTranslation(normalizedKey, value);
        }
    }

    /// <summary>Processes escape sequences: <c>\n</c>, <c>\t</c>, <c>\\</c>.</summary>
    internal static string UnescapeValue(string raw)
    {
        if (raw.IndexOf('\\') < 0) return raw;

        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                switch (raw[i + 1])
                {
                    case 'n': sb.Append('\n'); i++; continue;
                    case 't': sb.Append('\t'); i++; continue;
                    case '\\': sb.Append('\\'); i++; continue;
                }
            }
            sb.Append(raw[i]);
        }
        return sb.ToString();
    }

    // ── IETF extraction helpers ───────────────────────────────────────────────

    /// <summary>
    /// Extracts the IETF language tag from a language pack file path.
    /// Supports both <c>{basename}.{ietf}.ini</c> and <c>{basename}.{module}.{ietf}.ini</c>.
    /// Returns <c>null</c> when the file name does not match the expected pattern.
    /// </summary>
    private string? ExtractIetfFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath); // strips ".ini"
        if (fileName == null) return null;

        // File must start with the basename (case-insensitive)
        if (!fileName.StartsWith(_basename + ".", StringComparison.OrdinalIgnoreCase))
            return null;

        // Remainder after "{basename}."
        var remainder = fileName.Substring(_basename.Length + 1);
        if (string.IsNullOrEmpty(remainder)) return null;

        // Could be "{ietf}" or "{module}.{ietf}"
        // The last dot-separated segment is always the IETF tag.
        var dotIdx = remainder.LastIndexOf('.');
        return dotIdx >= 0 ? remainder.Substring(dotIdx + 1) : remainder;
    }

    private static bool TryGetCultureInfo(string ietf, out CultureInfo? culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(ietf);
            return true;
        }
        catch
        {
            culture = null;
            return false;
        }
    }

    // ── File monitoring ───────────────────────────────────────────────────────

    private void StartMonitoring()
    {
        var directories = _sections.Values
            .Select(e => e.Directory)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        _debounceTimer = new System.Threading.Timer(_ =>
        {
            if (!_disposed)
                ReloadCurrentLanguage();
        }, null, Timeout.Infinite, Timeout.Infinite);

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir) || _watchers.ContainsKey(dir)) continue;

            var watcher = new FileSystemWatcher(dir, "*.ini")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            watcher.Changed += OnFileChanged;
            _watchers[dir] = watcher;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
        => _debounceTimer?.Change(DebounceMs, Timeout.Infinite);

    private void ReloadCurrentLanguage()
    {
        LoadLanguage(_currentLanguage);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Line reader ───────────────────────────────────────────────────────────

    private static ReadOnlySpan<char> ReadLine(ref ReadOnlySpan<char> remaining)
    {
        var newLine = remaining.IndexOfAny('\r', '\n');
        if (newLine < 0)
        {
            var line = remaining;
            remaining = ReadOnlySpan<char>.Empty;
            return line;
        }

        var result = remaining.Slice(0, newLine);
        remaining = remaining.Slice(newLine + 1);
        if (!remaining.IsEmpty && remaining[0] == '\n')
            remaining = remaining.Slice(1);

        return result;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var timer = _debounceTimer;
        _debounceTimer = null;
        timer?.Change(Timeout.Infinite, Timeout.Infinite);
        timer?.Dispose();

        foreach (var watcher in _watchers.Values)
            watcher.Dispose();
        _watchers.Clear();
    }
}
