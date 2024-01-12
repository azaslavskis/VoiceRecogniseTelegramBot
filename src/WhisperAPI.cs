using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceRecogniseBot
{
     class WhisperAPI
    {
        
        public WhisperAPI() {
            var config = new Config();
            var ModelNameFromConfig = config.GetConfig()["model"];
            string modelName;
            if (ModelNameFromConfig != null)
            {
                 modelName = ModelNameFromConfig;
            }
            else
            {
                Console.WriteLine("fall back to default");
                 modelName = "ggml-base";
            }
           
            if (!File.Exists(modelName))
            {
                using var modelStream = WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base).Result;
                using var fileWriter = File.OpenWrite(modelName);
                modelStream.CopyTo(fileWriter);
            }

        }

        internal string RecogniseWav(string file,string lang)
        {
            Console.WriteLine($"Model is ok {lang}");
            using var whisperFactory = WhisperFactory.FromPath("ggml-base.bin");

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage(lang.ToLower())
                .Build();

            var convertor = new AudioToWav();

            var path = convertor.OggToWav(file);
            using var fileStream = File.OpenRead(path);

            var output = processor.ProcessAsync(fileStream);
            StringBuilder sb = new StringBuilder();
    

            foreach (var result in output.ToBlockingEnumerable())
            {
                string result_val = string.Format($"{result.Start}->{result.End}: {result.Text}");

                sb.AppendLine(result_val);
                Console.WriteLine(result_val);   
            }
            return sb.ToString();


        }

        
    }
}
