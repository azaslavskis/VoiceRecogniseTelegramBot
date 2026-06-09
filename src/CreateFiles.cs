using Newtonsoft.Json;
using NLog;
using NLog.Targets;

namespace VoiceRecogniseBot;

public sealed class AppConfiguration
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly SettingsPathClass SettingsPath = new();

    public AppConfiguration()
    {
        ConfigureLogging();
        EnsureApplicationFilesExist();
    }

    public void EnsureApplicationFilesExist()
    {
        try
        {
            SettingsPath.EnsureConfigDirectoryExists();
            EnsureConfigExists();
            EnsureStatsExists();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while preparing application files.");
            throw;
        }
    }

    private static void ConfigureLogging()
    {
        var logPath = Path.GetTempFileName();

        var fileTarget = new FileTarget("logfile")
        {
            FileName = logPath,
            Layout = "${longdate} ${level:uppercase=true} ${message} ${exception}"
        };

        var config = new NLog.Config.LoggingConfiguration();
        config.AddTarget(fileTarget);
        config.AddRuleForAllLevels(fileTarget);

        LogManager.Configuration = config;
        Logger.Info("Log stored at {0}", logPath);
    }

    private static void EnsureConfigExists()
    {
        var configPath = SettingsPath.GetSettingPath();
        if (File.Exists(configPath))
        {
            return;
        }

        var defaultConfig = new AppConfig();
        var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
        File.WriteAllText(configPath, json);
        Logger.Info("Created default configuration file at {0}", configPath);
    }

    private static void EnsureStatsExists()
    {
        var statsPath = SettingsPath.GetStatsPath();
        if (File.Exists(statsPath))
        {
            return;
        }

        var stats = new StatsData();
        var json = JsonConvert.SerializeObject(stats, Formatting.Indented);
        File.WriteAllText(statsPath, json);
        Logger.Info("Created stats file at {0}", statsPath);
    }
}
