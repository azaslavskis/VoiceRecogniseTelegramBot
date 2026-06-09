using NLog;
using NLog.Config;
using NLog.Targets;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace VoiceRecogniseBot;

public sealed class TelegramBotLogger
{
    private static readonly MemoryTarget MemoryTarget = new("memory")
    {
        Layout = "${message}"
    };

    private static bool _isConfigured;

    public Logger logger { get; }

    public TelegramBotLogger()
    {
        EnsureConfigured();
        logger = LogManager.GetCurrentClassLogger();
    }

    public string ReturnLogAsString()
    {
        return MemoryTarget.Logs.Count == 0
            ? "No log messages yet."
            : string.Join(Environment.NewLine, MemoryTarget.Logs).Normalize();
    }

    private static void EnsureConfigured()
    {
        if (_isConfigured)
        {
            return;
        }

        var logPath = Path.GetTempFileName();
        var config = new LoggingConfiguration();

        var fileTarget = new FileTarget("logfile")
        {
            FileName = logPath,
            Layout = "${longdate} ${level:uppercase=true} ${message} ${exception}"
        };

        config.AddTarget(fileTarget);
        //config.AddTarget(memoryTarget);

        config.LoggingRules.Add(
            new NLog.Config.LoggingRule("*", NLog.LogLevel.Debug, fileTarget)
        );


        LogManager.Configuration = config;
        _isConfigured = true;
    }
}
