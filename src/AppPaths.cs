namespace VoiceRecogniseBot;

/// <summary>
/// Resolves the directories and files used by the application at runtime.
/// </summary>
internal static class AppPaths
{
    private const string HomeVariable = "VOICE_RECOGNISEBOT_HOME";
    private const string AppFolder = "VoiceRecogniseBot";

    public static string DataDirectory
    {
        get
        {
            var customDirectory = Environment.GetEnvironmentVariable(HomeVariable);
            if (!string.IsNullOrWhiteSpace(customDirectory))
            {
                return customDirectory;
            }

            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppFolder);
            }

            if (OperatingSystem.IsLinux())
            {
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                return string.IsNullOrWhiteSpace(xdgConfigHome)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", AppFolder)
                    : Path.Combine(xdgConfigHome, AppFolder);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolder);
        }
    }

    public static string SettingsFile => Path.Combine(DataDirectory, "appsettings.json");

    public static string StatsFile => Path.Combine(DataDirectory, "stats.json");

    public static string LogFile => Path.Combine(DataDirectory, "logs", "bot.log");

    public static string ModelsDirectory => Path.Combine(DataDirectory, "models");

    public static string GetManagedModelPath(string modelName)
    {
        var fileName = modelName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? modelName
            : $"{modelName}.bin";

        return Path.Combine(ModelsDirectory, fileName);
    }
}
