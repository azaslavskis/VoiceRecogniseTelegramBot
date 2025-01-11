using System;
using NLog;
using NLog.Config;
using NLog.Targets;
namespace VoiceRecogniseBot
{
    public class TelegramBotLogger
    {
        private static string logPath;

        public NLog.Logger logger;

        private static MemoryTarget memoryTarget;

        public TelegramBotLogger()
        {
            // Create a new NLog configuration
            var config = new LoggingConfiguration();

            logPath = Path.GetTempFileName();

            // Create a memory target
            memoryTarget = new MemoryTarget("target1")
            {
                Layout = "${message}"
            };

            // Add the memory target to the configuration
            config.AddTarget(memoryTarget);

            // Create a file target
            var fileTarget = new FileTarget("logfile")
            {
                FileName = logPath
            };
            //Console.WriteLine(logPath);
            // Add the file target to the configuration
            config.AddTarget(fileTarget);
            config.AddTarget(memoryTarget);

            // Define a rule to log all messages with a minimum level of Info to the "logfile" target
            var rule = new LoggingRule("*", LogLevel.Info, fileTarget);
            var rule_memory = new LoggingRule("*", LogLevel.Info, memoryTarget);
            config.LoggingRules.Add(rule);
            config.LoggingRules.Add(rule_memory);
            // Apply the configuration
            LogManager.Configuration = config;

            // Create a logger and use it to log messages
            logger = LogManager.GetCurrentClassLogger();

        }

        public string ReturnLogAsString()
        {
            if (memoryTarget != null)
            {
                
                var loggedMessages = memoryTarget.Logs;
                var logs = string.Join(Environment.NewLine, loggedMessages).Normalize();
                return logs;
            }
            else
            {
                return "something went wrong";
            }
        }
    }




}

  
