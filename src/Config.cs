using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace VoiceRecogniseBot
{
    /// <summary>
    /// Provides access to configuration settings.
    /// </summary>
    internal class Config
    {

        /// <summary>
        /// Retrieves the application configuration settings.
        /// </summary>
        /// <returns>An instance of <see cref="IConfigurationRoot"/> containing the configuration settings.</returns>
        /// 
        private static TelegramBotLogger app_log = new TelegramBotLogger();
        internal IConfigurationRoot GetConfig()
        {
            app_log.logger.Debug($"Requested config:\"appsettings.json\"");
            // Create a configuration builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                //.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("/etc/voicerecognisebot/appsettings.json", optional: false, reloadOnChange: true);
            // Build and return the configuration
            IConfigurationRoot configuration = builder.Build();
            return configuration;
        }

        internal void UpdateConfig(IConfigurationRoot configuration)
        {
            JObject jsonConfig = JObject.Parse(File.ReadAllText("/etc/voicerecognisebot/appsettings.json"));
            app_log.logger.Debug($"Reading all json {jsonConfig}");
           // Console.WriteLine($"Reading all json {jsonConfig}");
            // Loop through all properties in the JObject
            foreach (var property in jsonConfig.Properties())
            {
                // Check if the property name matches the key in your configuration
                if (configuration[property.Name] != null)
                {
                    // Update the property value with the corresponding configuration value
                    property.Value = new JValue(configuration[property.Name]);
                    app_log.logger.Debug($"Updated Property Name: {property.Name}, Value: {property.Value}");
                }
            }


            // Write the updated JObject back to the JSON file
            File.WriteAllText("/etc/voicerecognisebot/appsettings.json", jsonConfig.ToString());
            app_log.logger.Debug($"\"Configuration updated.\"");
            Console.WriteLine("Configuration updated.");

  
        }
    }
}
