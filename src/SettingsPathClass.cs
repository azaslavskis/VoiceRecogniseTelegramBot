using System.Runtime.InteropServices;

namespace VoiceRecogniseBot;

/// <summary>
/// Resolves the directories used by the application for config and stats files.
/// </summary>
public sealed class SettingsPathClass
{
    private const string EnvironmentVariableName = "VOICE_RECOGNISEBOT_HOME";

    public string GetConfigDirectory()
    {
        var customDirectory = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(customDirectory))
        {
            return customDirectory;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VoiceRecogniseBot");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdgConfigHome))
            {
                return Path.Combine(xdgConfigHome, "VoiceRecogniseBot");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "VoiceRecogniseBot");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceRecogniseBot");
    }

    public string GetSettingPath()
    {
        return Path.Combine(GetConfigDirectory(), "appsettings.json");
    }

    public string GetStatsPath()
    {
        return Path.Combine(GetConfigDirectory(), "stats.json");
    }

    public string GetModelsDirectory()
    {
        return Path.Combine(GetConfigDirectory(), "models");
    }

    public string GetManagedModelPath(string modelName)
    {
        var sanitizedFileName = modelName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? modelName
            : $"{modelName}.bin";

        return Path.Combine(GetModelsDirectory(), sanitizedFileName);
    }

    public void EnsureConfigDirectoryExists()
    {
        Directory.CreateDirectory(GetConfigDirectory());
    }

    public void EnsureModelsDirectoryExists()
    {
        Directory.CreateDirectory(GetModelsDirectory());
    }
}
