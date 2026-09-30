using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace VoiceRecogniseBot;

/// <summary>
/// Application-wide logging. Everything goes to a rolling file in the data directory;
/// long-running commands also mirror messages to the console.
/// </summary>
internal static class AppLog
{
    private const string Layout = "${longdate} ${level:uppercase=true} ${message}${onexception:inner= ${exception:format=tostring}}";
    private const int TailWindowBytes = 256 * 1024;

    public static Logger Logger { get; } = LogManager.GetLogger("VoiceRecogniseBot");

    public static void Configure(bool logToConsole)
    {
        var config = new LoggingConfiguration();

        var fileTarget = new FileTarget("logfile")
        {
            FileName = AppPaths.LogFile,
            Layout = Layout,
            Encoding = Encoding.UTF8,
            // The bot and the web UI can run as separate processes sharing this file.
            KeepFileOpen = false,
            ArchiveAboveSize = 2 * 1024 * 1024,
            MaxArchiveFiles = 3
        };
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);

        if (logToConsole)
        {
            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${level:uppercase=true} ${message}${onexception:inner= ${exception:format=message}}"
            };
            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
        }

        LogManager.Configuration = config;
    }

    /// <summary>
    /// Returns the most recent lines of the log file, oldest first.
    /// </summary>
    public static IReadOnlyList<string> ReadTail(int maxLines)
    {
        if (!File.Exists(AppPaths.LogFile))
        {
            return [];
        }

        using var stream = new FileStream(AppPaths.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var truncated = stream.Length > TailWindowBytes;
        if (truncated)
        {
            stream.Seek(-TailWindowBytes, SeekOrigin.End);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // After seeking into the middle of the file the first line is almost certainly partial.
        return lines
            .Skip(truncated ? 1 : 0)
            .Select(line => line.TrimEnd('\r'))
            .TakeLast(maxLines)
            .ToList();
    }
}
