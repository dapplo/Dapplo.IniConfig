// Copyright (c) Dapplo. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Dapplo.Ini.Interfaces;

/// <summary>
/// Listener interface for receiving diagnostic events from the Dapplo.Ini framework.
/// Implement this interface in your own class, then register the instance via
/// <see cref="Configuration.IniConfigBuilder.AddListener"/>.
/// </summary>
/// <remarks>
/// There is zero overhead when no listener is registered, and minimal overhead when one or
/// more are registered (a single <c>Count == 0</c> guard is all that is evaluated on each
/// operation when the listener list is empty).
/// No logging framework dependency is introduced by this interface.
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyLogger : IIniConfigListener
/// {
///     public void OnFileLoaded(string filePath)   => Log.Info($"INI loaded: {filePath}");
///     public void OnFileNotFound(string fileName) => Log.Warn($"INI file not found: {fileName}, using defaults");
///     public void OnSaved(string filePath)        => Log.Info($"INI saved: {filePath}");
///     public void OnReloaded(string filePath)     => Log.Info($"INI reloaded: {filePath}");
///     public void OnError(string operation, Exception exception) => Log.Error($"INI {operation} error", exception);
/// }
///
/// IniConfigRegistry.ForFile("app.ini")
///     .AddSearchPath(dir)
///     .RegisterSection&lt;IMySettings&gt;(section)
///     .AddListener(new MyLogger())
///     .Build();
/// </code>
/// </example>
public interface IIniConfigListener
{
    /// <summary>
    /// Called after the INI file has been successfully found and its values applied to sections.
    /// </summary>
    /// <param name="filePath">The absolute path of the file that was loaded.</param>
    void OnFileLoaded(string filePath);

    /// <summary>
    /// Called when the INI file could not be found at any of the configured search paths.
    /// All sections fall back to their compiled default values.
    /// </summary>
    /// <param name="fileName">The file name that was searched for (e.g. <c>"app.ini"</c>).</param>
    void OnFileNotFound(string fileName);

    /// <summary>
    /// Called after the configuration has been successfully written to disk.
    /// </summary>
    /// <param name="filePath">The absolute path of the file that was written.</param>
    void OnSaved(string filePath);

    /// <summary>
    /// Called after the configuration has been successfully reloaded from disk
    /// (triggered by <see cref="Configuration.IniConfig.Reload"/> or an external file-change).
    /// </summary>
    /// <param name="filePath">The absolute path of the file that was reloaded.</param>
    void OnReloaded(string filePath);

    /// <summary>
    /// Called when an exception is thrown during a load, reload, or save operation.
    /// The exception is always re-thrown after all listeners have been notified.
    /// </summary>
    /// <param name="operation">
    /// A short description of the operation that failed (e.g. <c>"Load"</c>, <c>"Save"</c>,
    /// <c>"Reload"</c>).
    /// </param>
    /// <param name="exception">The exception that was thrown.</param>
    void OnError(string operation, Exception exception);
}
