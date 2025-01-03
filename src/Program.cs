using System;
using CommandLine;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;
using NLog;
using Velopack;

namespace VoiceRecogniseBot
{
    public class Program
    {
        private static Config config = new Config();
        private static TelegramBotLogger app_log = new TelegramBotLogger();
        public class Options
        {
            [Option('c', "command", Required = false, HelpText = "Specify the command to run.")]
            public required string Command { get; set; }

            // Add other options for configuration edit commands here if needed
            [Option('m', "model", Required = false, HelpText = "Set the model name.")]
            public required string Model { get; set; }

            [Option('t', "token", Required = false, HelpText = "Set the token.")]
            public required string Token { get; set; }

            [Option('l', "lang", Required = false, HelpText = "Set the languages.")]
            public required string Lang { get; set; }

            [Option('d', "default-lang", Required = false, HelpText = "Set the default language.")]
            public required string DefaultLang { get; set; }
        }

        public static void Main(string[] args)
        {

            
            VelopackApp.Build().Run();
            app_log.logger.Debug("First message from logger");
            AppConfiguration appconfig = new AppConfiguration();
            appconfig.BuildConfiguration();
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (!string.IsNullOrWhiteSpace(o.Command))
                    {
                        // Check for specific commands and handle them accordingly
                        switch (o.Command.ToLower())
                        {
                            case "bot":
                                RunBot();
                                break;
                            case "update_config":
                                UpdateConfiguration(o);
                                break;
                            default:
                                Console.WriteLine("Invalid command. Supported commands: bot, update_config");
                                app_log.logger.Error($"Invalid command passed {o.Command.ToLower()}");
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Command is requred");
                        Console.WriteLine("Supported commands: bot, update_config");
                        app_log.logger.Error($"No command passed {o.Command}");
                    }
                });
        }

        private static void RunBot()
        {
            var telegram = new TelegramApi();
        }

        private static void UpdateConfiguration(Options options)
        {
            // Implement logic to update the configuration based on the provided options
            Console.WriteLine("Updating configuration...");
            var config_in_use = config.GetConfig();
            app_log.logger.Info($"Config update {options}");


            if (!string.IsNullOrEmpty(options.Model))
            {
                // Update the model configuration
                app_log.logger.Info($"Updating model: {options.Model}");
                // Add code to update the model configuration here
                if (config_in_use["model"] != null)
                {
                    config_in_use["model"] = options.Model;


                }
                config.UpdateConfig(config_in_use);
            }

            if (!string.IsNullOrEmpty(options.Token))
            {
                // Update the token configuration
                app_log.logger.Debug($"Updating model: {options.Token}");

                if (config_in_use["token"] != null)
                {
                    config_in_use["token"] = options.Token;

                }
                config.UpdateConfig(config_in_use);

            }

            if (!string.IsNullOrEmpty(options.Lang))
            {
                // Update the languages configuration
                app_log.logger.Info($"Updating model: {options.Lang}");
         
                if (config_in_use["lang"] != null)
                {
                    config_in_use["lang"] = options.Lang;

                }
                config.UpdateConfig(config_in_use);
            }

            if (!string.IsNullOrEmpty(options.DefaultLang))
            {
                // Update the default language configuration
                app_log.logger.Info($"Updating model: {options.DefaultLang}");

                if (config_in_use["default_lang"] != null)
                {
                    Console.WriteLine($"Updating default language: {config_in_use["default_lang"]}");
                    config_in_use["default_lang"] = options.DefaultLang;
                    

             
                }
                config.UpdateConfig(config_in_use);
            }

        }
    }
}