using System;
using System.IO;
using System.Runtime.InteropServices;
using NLog;

namespace VoiceRecogniseBot
{
    /// <summary>
    /// A utility class to provide platform-specific paths for application settings and statistics files.
    /// </summary>
    public class SettingsPathClass
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Constants for file paths on different platforms
        private const string WindowsSettingPath = "C:\\VoiceRecogniseBotConfig\\appsettings.json";
        private const string LinuxSettingPath = "/etc/voicerecognise/appsettings.json";
        private const string WindowsStatsPath = "C:\\VoiceRecogniseBotConfig\\stats.json";
        private const string LinuxStatsPath = "/etc/voicerecognise/stats.json";

        /// <summary>
        /// Gets the platform-specific path for the settings file.
        /// </summary>
        /// <returns>The path to the settings file for the current platform.</returns>
        public string GetSettingPath()
        {
            return GetPath("appsettings.json", WindowsSettingPath, LinuxSettingPath);
        }

        /// <summary>
        /// Gets the platform-specific path for the statistics file.
        /// </summary>
        /// <returns>The path to the statistics file for the current platform.</returns>
        public string GetStatsPath()
        {
            return GetPath("stats.json", WindowsStatsPath, LinuxStatsPath);
        }

        /// <summary>
        /// Determines the platform-specific path for a given file.
        /// </summary>
        /// <param name="fileName">The name of the file (e.g., "appsettings.json").</param>
        /// <param name="windowsPath">The default path for Windows.</param>
        /// <param name="linuxPath">The default path for Linux.</param>
        /// <returns>The resolved file path based on the current platform.</returns>
        private string GetPath(string fileName, string windowsPath, string linuxPath)
        {
            try
            {
                // Check if the current platform is Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine(windowsPath);
                    return windowsPath;
                }

                // Check if the current platform is Linux
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Console.WriteLine(linuxPath);
                    return linuxPath;
                }

                // Check if the current platform is macOS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // For macOS, construct the path within the ApplicationData directory
                    string macOsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "voicerecognise", fileName);
                    Console.WriteLine(macOsPath);
                    return macOsPath;
                }

                // Log an error if the platform is unsupported
                Logger.Error($"Unsupported platform: {RuntimeInformation.OSDescription}");
            }
            catch (Exception ex)
            {
                // Log the exception details
                Logger.Error(ex, "Error determining the settings path");
            }

            // Return a default fallback path if no platform matches or an error occurs
            return fileName;
        }
    }
}
