using System.Text.Encodings.Web;
using System.Text.Json;

namespace VoiceRecogniseBot;

/// <summary>
/// Reads and writes the application's JSON configuration file.
/// </summary>
internal static class ConfigStore
{
    public const string TokenPlaceholder = "xxxx";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Keep non-ASCII bot texts and apostrophes readable in the file instead of escaping them.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lock Sync = new();
    private static AppConfig? _cached;
    private static DateTime _cachedWriteTimeUtc;

    /// <summary>
    /// Returns the current configuration, re-reading the file when it changed on disk.
    /// The bot and the web UI may run as separate processes, so the file is the source of truth.
    /// </summary>
    public static AppConfig Load()
    {
        lock (Sync)
        {
            EnsureExists();

            var writeTimeUtc = File.GetLastWriteTimeUtc(AppPaths.SettingsFile);
            if (_cached is not null && writeTimeUtc == _cachedWriteTimeUtc)
            {
                return _cached;
            }

            try
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                Normalize(config);

                _cached = config;
                _cachedWriteTimeUtc = writeTimeUtc;
            }
            catch (Exception ex) when (ex is JsonException or IOException && _cached is not null)
            {
                // Keep running on the last good configuration if the file is mid-write or hand-edited badly.
                AppLog.Logger.Warn(ex, "Could not read {0}; keeping the previous configuration", AppPaths.SettingsFile);
            }

            return _cached!;
        }
    }

    public static void Save(AppConfig config)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(config, JsonOptions));
            _cached = null;
        }

        AppLog.Logger.Info("Configuration updated at {0}", AppPaths.SettingsFile);
    }

    public static void EnsureExists()
    {
        if (File.Exists(AppPaths.SettingsFile))
        {
            return;
        }

        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(new AppConfig(), JsonOptions));
        AppLog.Logger.Info("Created default configuration file at {0}", AppPaths.SettingsFile);
    }

    public static bool IsTokenConfigured(string? token)
    {
        return !string.IsNullOrWhiteSpace(token) &&
               !string.Equals(token.Trim(), TokenPlaceholder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trims values and fills in anything a hand-edited or partial config left out.
    /// </summary>
    public static void Normalize(AppConfig config)
    {
        config.Model = string.IsNullOrWhiteSpace(config.Model) ? AppConfig.DefaultModel : config.Model.Trim();
        config.Token = config.Token?.Trim() ?? string.Empty;
        config.BotText ??= new BotTextConfig();

        config.Lang = (config.Lang ?? [])
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        config.DefaultLang = config.DefaultLang?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Returns a user-facing error when the configuration cannot be saved, otherwise null.
    /// </summary>
    public static string? Validate(AppConfig config)
    {
        if (config.Lang.Count == 0)
        {
            return "At least one recognition language is required.";
        }

        if (!config.Lang.Contains(config.DefaultLang))
        {
            return "Default language must be included in the recognition languages.";
        }

        return WhisperApi.DescribeModelProblem(config.Model);
    }
}
