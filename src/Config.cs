using System;
using System.IO;
using Microsoft.Extensions.Configuration;

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
    }
}
