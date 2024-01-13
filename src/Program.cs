using System;
using CommandLine;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;

namespace VoiceRecogniseBot
{
    public class Program
    {
        private static Config config = new Config();
        public class Options
        {
            [Option('c', "command", Required = false, HelpText = "Specify the command to run.")]
            public string Command { get; set; }

            // Add other options for configuration edit commands here if needed
            [Option('m', "model", Required = false, HelpText = "Set the model name.")]
            public string Model { get; set; }

            [Option('t', "token", Required = false, HelpText = "Set the token.")]
            public string Token { get; set; }

            [Option('l', "lang", Required = false, HelpText = "Set the languages.")]
            public string Lang { get; set; }

            [Option('d', "default-lang", Required = false, HelpText = "Set the default language.")]
            public string DefaultLang { get; set; }
        }

        public static void Main(string[] args)
        {

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
                                break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Command is requred");
                        Console.WriteLine("Supported commands: bot, update_config");
                    }
                });
        }

        private static void RunBot()
        {
            var telegram = new TelegramAPI();
            
        }

        private static void UpdateConfiguration(Options options)
        {
            // Implement logic to update the configuration based on the provided options
            Console.WriteLine("Updating configuration...");
            var config_in_use = config.GetConfig();


            if (!string.IsNullOrEmpty(options.Model))
            {
                // Update the model configuration
                Console.WriteLine($"Updating model: {options.Model}");
                // Add code to update the model configuration here
                if (config_in_use["model"] != null)
                {
                    config_in_use["model"] = options.Model;


                }
            }

            if (!string.IsNullOrEmpty(options.Token))
            {
                // Update the token configuration
                Console.WriteLine($"Updating token: {options.Token}");

                if (config_in_use["token"] != null)
                {
                    config_in_use["token"] = options.Token;

                }
                // Add code to update the token configuration here
            }

            if (!string.IsNullOrEmpty(options.Lang))
            {
                // Update the languages configuration
                Console.WriteLine($"Updating languages: {options.Lang}");
                // Add code to update the languages configuration here
                if (config_in_use["lang"] != null)
                {
                    config_in_use["lang"] = options.Lang;

                }
            }

            if (!string.IsNullOrEmpty(options.DefaultLang))
            {
                // Update the default language configuration
                Console.WriteLine($"Updating default language: {options.DefaultLang}");
            
                if (config_in_use["default_lang"] != null)
                {
                    Console.WriteLine($"Updating default language: {config_in_use["default_lang"]}");
                    config_in_use["default_lang"] = options.DefaultLang;
                    

                    // Add code to update the default language configuration here
                }



                config.UpdateConfig(config_in_use);

                // Add your configuration update logic here
            }
        }
    }
}