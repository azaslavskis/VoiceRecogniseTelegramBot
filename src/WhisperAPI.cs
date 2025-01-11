using System;
using System.IO;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceRecogniseBot
{
    /// <summary>
    /// Provides functionality to interact with the Whisper API for speech recognition.
    /// </summary>
    class WhisperAPI
    {
        private static TelegramBotLogger app_log = new TelegramBotLogger();
        private string modelName;
        /// <summary>
        /// Converts a model name to its corresponding GgmlType.
        /// </summary>
        /// <param name="modelName">The model name to convert.</param>
        /// <returns>The GgmlType corresponding to the given model name, or GgmlType.Base if no match is found.</returns>
        public GgmlType ToModel(string modelName)
        {
            // Create a dictionary to map model names to GgmlType values
            Dictionary<string, GgmlType> modelMapping = new Dictionary<string, GgmlType>
    {
        { GgmlType.Tiny.ToString().ToLower(), GgmlType.Tiny },
        { GgmlType.Small.ToString().ToLower(), GgmlType.Small },
        { GgmlType.Medium.ToString().ToLower(), GgmlType.Medium },
        { GgmlType.LargeV1.ToString().ToLower(), GgmlType.LargeV1 },
        { GgmlType.LargeV2.ToString().ToLower(), GgmlType.LargeV2 },
        { GgmlType.LargeV3.ToString().ToLower(), GgmlType.LargeV3 },
        { GgmlType.Base.ToString().ToLower(), GgmlType.Base }
    };

            // Check if the modelName (case insensitive) exists in the dictionary, then return the corresponding GgmlType
            if (modelMapping.ContainsKey(modelName.ToLower()))
            {
                return modelMapping[modelName.ToLower()];
            }

            // If no match is found, return GgmlType.Base by default
            return GgmlType.Base;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperAPI"/> class.
        /// </summary>
        public WhisperAPI()
        {
            // Initialize Whisper API configuration
            var config = new Config();
            var modelNameFromConfig = config.GetConfig()["model"];
            app_log.logger.Debug($"Model from config {modelNameFromConfig}");


            // Check if a custom model name is specified in the configuration
            if (modelNameFromConfig != null)
            {
                modelName = modelNameFromConfig;
            }
            else
            {
                Console.WriteLine("Fallback to default model");
                modelName = "ggml-base";
            }
            app_log.logger.Debug($"Model name recognised from config {modelName}");
            // Download and save the model if it doesn't exist
            if (!File.Exists(modelName))
            {
                app_log.logger.Debug($"Clone model {modelName}");
                using var modelStream = WhisperGgmlDownloader.GetGgmlModelAsync(ToModel(modelName)).Result;
                using var fileWriter = File.OpenWrite(modelName);
                modelStream.CopyTo(fileWriter);
            }
        }

        /// <summary>
        /// Recognizes text from an Ogg audio file using the specified language.
        /// </summary>
        /// <param name="file">The path to the Ogg audio file.</param>
        /// <param name="lang">The language for speech recognition.</param>
        /// <returns>The recognized text.</returns>
        internal string? RecogniseWav(string file, string? lang)
        {
            app_log.logger.Debug($"Model is ok! Lang in use: {lang}");
            using var whisperFactory = WhisperFactory.FromPath(modelName);

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(lang.ToLower())
                .Build();

            var converter = new AudioToWav();
            app_log.logger.Debug($"Convert file to wav 16khz {file}");

            var path = converter.OggToWav(file);
            using var fileStream = File.OpenRead(path);

            var output = processor.ProcessAsync(fileStream);
            StringBuilder sb = new StringBuilder();
            app_log.logger.Debug($"Perparing string buffer for file {file}");

            foreach (var result in output.ToBlockingEnumerable())
            {
                string resultValue = $"{result.Start}->{result.End}: {result.Text}";

                sb.AppendLine(resultValue);
                app_log.logger.Debug($"Recognised value {resultValue}");
            }
            app_log.logger.Info($"Function is ok! File {file} is parsed");
            return sb.ToString();
        }

        /// <summary>
        /// Recognizes text from an MP4 audio file using the specified language.
        /// </summary>
        /// <param name="file">The path to the MP4 audio file.</param>
        /// <param name="lang">The language for speech recognition.</param>
        /// <returns>The recognized text.</returns>
        internal string? RecogniseMp4(string file, string? lang)
        {
            app_log.logger.Debug($"Model is ok! Lang in use: {lang}");
            using var whisperFactory = WhisperFactory.FromPath(modelName);

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(lang.ToLower())
                .Build();

            var converter = new AudioToWav();
            app_log.logger.Debug($"Convert file to wav 16khz {file}");
            var path = converter.Mp4ToWav(file);
            if (path.Contains("error"))
            {
                return "Video is not supported yet under Linux";

            }
            else
            {
                using var fileStream = File.OpenRead(path);

                var output = processor.ProcessAsync(fileStream);
                StringBuilder sb = new StringBuilder();
                app_log.logger.Debug($"Perparing string buffer for file {file}");

                foreach (var result in output.ToBlockingEnumerable())
                {
                    string resultValue = $"{result.Start}->{result.End}: {result.Text}";

                    sb.AppendLine(resultValue);
                    app_log.logger.Info($"Function is ok! File {file} is parsed");
                }

                app_log.logger.Info($"Function is ok! File {file} is parsed");
                return sb.ToString();
            }
        }
    }
}
