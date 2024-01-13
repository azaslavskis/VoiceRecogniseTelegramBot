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
        internal IConfigurationRoot GetConfig()
        {
            // Create a configuration builder
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            // Build and return the configuration
            IConfigurationRoot configuration = builder.Build();
            return configuration;
        }

        internal void UpdateConfig(IConfigurationRoot configuration)
        {
            JObject jsonConfig = JObject.Parse(File.ReadAllText("appsettings.json"));


            // Loop through all properties in the JObject
            foreach (var property in jsonConfig.Properties())
            {
                // Check if the property name matches the key in your configuration
                if (configuration[property.Name] != null)
                {
                    // Update the property value with the corresponding configuration value
                    property.Value = new JValue(configuration[property.Name]);
                    Console.WriteLine($"Updated Property Name: {property.Name}, Value: {property.Value}");
                }
            }


            // Write the updated JObject back to the JSON file
            File.WriteAllText("appsettings.json", jsonConfig.ToString());

            Console.WriteLine("Configuration updated.");

  
        }
    }
}
