

using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Targets;

namespace VoiceRecogniseBot
{
    public class AppConfiguration
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static  SettingsPathClass settingsPath = new SettingsPathClass();

        public IConfigurationRoot? Configuration { get; private set; }
        
        public AppConfiguration()
        {
            ConfigureLogging();
            Logger.Debug("Starting configuration setup...");

            EnsureConfigFilesExist();

            //Configuration = BuildConfiguration();

            Logger.Debug("Configuration setup completed.");
        }

        private void ConfigureLogging()
        {
            var path_log = Path.GetTempFileName();
            Console.WriteLine($"Log stored: {path_log}");
            var fileTarget = new FileTarget("logfile")
            {
                FileName = path_log,
                Layout = "${longdate} ${level:uppercase=true} ${message} ${exception}"
            };

            var config = new NLog.Config.LoggingConfiguration();
            config.AddTarget(fileTarget);
            config.AddRuleForAllLevels(fileTarget);

            LogManager.Configuration = config;
        }

        private void EnsureConfigFilesExist()
        {
            try
            {

                var filePath = settingsPath.GetSettingPath();

                    if (!File.Exists(filePath))
                    {
                        Logger.Warn($"Configuration file not found: {filePath}. Creating with default settings.");

                        var directory = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                            Logger.Info($"Created directory: {directory}");
                        }

                        File.WriteAllText(filePath, """ 
                                                         {
                                                      "model": "ggml-base",
                                                      "token": "xxxx",
                                                      "lang": [
                                                        "RU",
                                                        "LV",
                                                        "EN"
                                                      ],
                                                      "default_lang": "EN"
                                                    } 
                                                    """);

                        Logger.Info($"Created default configuration file: {filePath}");
                    }
                
                
                
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while ensuring configuration files exist.");
                throw;
            }
        }


        public IConfigurationRoot BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile(settingsPath.GetSettingPath(), optional: true, reloadOnChange: true);

                return builder.Build();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while building configuration.");
                throw;
            }
        }
    }
}
