using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace VoiceRecogniseBot;

/// <summary>
/// Reads and writes the application's JSON configuration file.
/// </summary>
internal sealed class Config
{
    private static readonly TelegramBotLogger AppLog = new();
    private static readonly SettingsPathClass SettingsPath = new();

    internal IConfigurationRoot GetConfig()
    {
        AppLog.logger.Debug("Loading configuration from {0}", SettingsPath.GetSettingPath());

        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(SettingsPath.GetSettingPath(), optional: false, reloadOnChange: true)
            .Build();
    }

    internal AppConfig LoadAppConfig()
    {
        var json = File.ReadAllText(SettingsPath.GetSettingPath());
        return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
    }

    internal void SaveAppConfig(AppConfig config)
    {
        SettingsPath.EnsureConfigDirectoryExists();
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(SettingsPath.GetSettingPath(), json);
        AppLog.logger.Info("Configuration updated at {0}", SettingsPath.GetSettingPath());
    }

    public static string GetConfigContent()
    {
        return File.ReadAllText(SettingsPath.GetSettingPath());
    }
    
}
