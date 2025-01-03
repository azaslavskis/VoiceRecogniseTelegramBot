using System;
using NLog;
using NLog.Config;
using NLog.Targets;
namespace VoiceRecogniseBot
{
    public class TelegramBotLogger
    {

        public NLog.Logger logger;

        public TelegramBotLogger() {
            // Create a new NLog configuration
            var config = new LoggingConfiguration();

            // Create a file target
            var fileTarget = new FileTarget("logfile")
            {
                FileName = "/var/log/voicerecognisebot/log.txt"
            };

            // Add the file target to the configuration
            config.AddTarget(fileTarget);

            // Define a rule to log all messages with a minimum level of Info to the "logfile" target
            var rule = new LoggingRule("*", LogLevel.Info, fileTarget);
            config.LoggingRules.Add(rule);

            // Apply the configuration
            LogManager.Configuration = config;

            // Create a logger and use it to log messages
            logger = LogManager.GetCurrentClassLogger();
            


        }
    }
}

  
