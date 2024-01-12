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
        /// <summary>
        /// Initializes a new instance of the <see cref="WhisperAPI"/> class.
        /// </summary>
        public WhisperAPI()
        {
            // Initialize Whisper API configuration
            var config = new Config();
            var modelNameFromConfig = config.GetConfig()["model"];
            string modelName;

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

            // Download and save the model if it doesn't exist
            if (!File.Exists(modelName))
            {
                using var modelStream = WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base).Result;
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
        internal string RecogniseWav(string file, string lang)
        {
            Console.WriteLine($"Model is ok {lang}");
            using var whisperFactory = WhisperFactory.FromPath("ggml-base.bin");

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(lang.ToLower())
                .Build();

            var converter = new AudioToWav();

            var path = converter.OggToWav(file);
            using var fileStream = File.OpenRead(path);

            var output = processor.ProcessAsync(fileStream);
            StringBuilder sb = new StringBuilder();

            foreach (var result in output.ToBlockingEnumerable())
            {
                string resultValue = $"{result.Start}->{result.End}: {result.Text}";

                sb.AppendLine(resultValue);
                Console.WriteLine(resultValue);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Recognizes text from an MP4 audio file using the specified language.
        /// </summary>
        /// <param name="file">The path to the MP4 audio file.</param>
        /// <param name="lang">The language for speech recognition.</param>
        /// <returns>The recognized text.</returns>
        internal string RecogniseMp4(string file, string lang)
        {
            Console.WriteLine($"Model is ok {lang}");
            using var whisperFactory = WhisperFactory.FromPath("ggml-base.bin");

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(lang.ToLower())
                .Build();

            var converter = new AudioToWav();

            var path = converter.Mp4ToWav(file);
            using var fileStream = File.OpenRead(path);

            var output = processor.ProcessAsync(fileStream);
            StringBuilder sb = new StringBuilder();

            foreach (var result in output.ToBlockingEnumerable())
            {
                string resultValue = $"{result.Start}->{result.End}: {result.Text}";

                sb.AppendLine(resultValue);
                Console.WriteLine(resultValue);
            }

            return sb.ToString();
        }
    }
}
