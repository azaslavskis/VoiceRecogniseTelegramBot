using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceRecogniseBot;

/// <summary>
/// Provides functionality to interact with Whisper for speech recognition.
/// </summary>
internal sealed class WhisperAPI
{
    private static readonly TelegramBotLogger AppLog = new();
    private static readonly SettingsPathClass SettingsPath = new();

    private readonly string _modelName;
    private readonly string _modelPath;

    public WhisperAPI()
    {
        var config = new Config().LoadAppConfig();
        _modelName = string.IsNullOrWhiteSpace(config.Model) ? "ggml-base" : config.Model.Trim();
        _modelPath = ResolveModelPath(_modelName);

        AppLog.logger.Debug("Configured Whisper model alias/path: {0}", _modelName);
        AppLog.logger.Debug("Resolved Whisper model path: {0}", _modelPath);
        EnsureModelExists();
    }

    internal string RecogniseVoiceFile(string filePath, string? lang)
    {
        var converter = new AudioToWav();
        var wavPath = converter.ConvertToWav(filePath);

        try
        {
            using var fileStream = File.OpenRead(wavPath);
            return ProcessAudio(fileStream, filePath, lang);
        }
        finally
        {
            TryDeleteTempFile(wavPath);
        }
    }

    internal string RecogniseAudioFile(string filePath, string? lang)
    {
        return RecogniseGenericMediaFile(filePath, lang);
    }

    internal string RecogniseVideoFile(string filePath, string? lang)
    {
        return RecogniseGenericMediaFile(filePath, lang);
    }

    private string RecogniseGenericMediaFile(string filePath, string? lang)
    {
        var converter = new AudioToWav();
        var wavPath = converter.ConvertToWav(filePath);

        try
        {
            using var fileStream = File.OpenRead(wavPath);
            return ProcessAudio(fileStream, filePath, lang);
        }
        finally
        {
            TryDeleteTempFile(wavPath);
        }
    }

    private string ProcessAudio(Stream fileStream, string sourceFile, string? lang)
    {
        var language = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim().ToLowerInvariant();
        AppLog.logger.Debug("Preparing Whisper processor with language {0}", language);

        using var whisperFactory = WhisperFactory.FromPath(_modelPath);
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        var output = processor.ProcessAsync(fileStream);
        var builder = new StringBuilder();

        foreach (var result in output.ToBlockingEnumerable())
        {
            var line = $"{result.Start}->{result.End}: {result.Text}";
            builder.AppendLine(line);
            AppLog.logger.Debug("Recognised value {0}", line);
        }

        AppLog.logger.Info("Transcription completed for {0}", sourceFile);
        return builder.ToString();
    }

    private void EnsureModelExists()
    {
        if (File.Exists(_modelPath))
        {
            return;
        }

        SettingsPath.EnsureModelsDirectoryExists();
        AppLog.logger.Info("Downloading Whisper model {0} to {1}", _modelName, _modelPath);
        using var modelStream = WhisperGgmlDownloader.Default.GetGgmlModelAsync(ToModel(_modelName)).Result;
        using var fileWriter = File.OpenWrite(_modelPath);
        modelStream.CopyTo(fileWriter);
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }

    private static GgmlType ToModel(string modelName)
    {
        var normalizedName = modelName.Trim().ToLowerInvariant();
        var modelMapping = new Dictionary<string, GgmlType>
        {
            { "ggml-tiny", GgmlType.Tiny },
            { GgmlType.Tiny.ToString().ToLowerInvariant(), GgmlType.Tiny },
            { "ggml-tiny.en", GgmlType.TinyEn },
            { GgmlType.TinyEn.ToString().ToLowerInvariant(), GgmlType.TinyEn },
            { "ggml-base", GgmlType.Base },
            { "ggml-base.en", GgmlType.BaseEn },
            { GgmlType.Base.ToString().ToLowerInvariant(), GgmlType.Base },
            { GgmlType.BaseEn.ToString().ToLowerInvariant(), GgmlType.BaseEn },
            { "ggml-small", GgmlType.Small },
            { GgmlType.Small.ToString().ToLowerInvariant(), GgmlType.Small },
            { "ggml-small.en", GgmlType.SmallEn },
            { GgmlType.SmallEn.ToString().ToLowerInvariant(), GgmlType.SmallEn },
            { "ggml-medium", GgmlType.Medium },
            { GgmlType.Medium.ToString().ToLowerInvariant(), GgmlType.Medium },
            { "ggml-medium.en", GgmlType.MediumEn },
            { GgmlType.MediumEn.ToString().ToLowerInvariant(), GgmlType.MediumEn },
            { "ggml-large-v1", GgmlType.LargeV1 },
            { GgmlType.LargeV1.ToString().ToLowerInvariant(), GgmlType.LargeV1 },
            { "ggml-large-v2", GgmlType.LargeV2 },
            { GgmlType.LargeV2.ToString().ToLowerInvariant(), GgmlType.LargeV2 },
            { "ggml-large-v3", GgmlType.LargeV3 },
            { GgmlType.LargeV3.ToString().ToLowerInvariant(), GgmlType.LargeV3 },
            { "ggml-large-v3-turbo", GgmlType.LargeV3Turbo },
            { GgmlType.LargeV3Turbo.ToString().ToLowerInvariant(), GgmlType.LargeV3Turbo }
        };

        return modelMapping.TryGetValue(normalizedName, out var resolvedModel)
            ? resolvedModel
            : GgmlType.Base;
    }

    private static string ResolveModelPath(string modelName)
    {
        if (Path.IsPathRooted(modelName) ||
            modelName.Contains(Path.DirectorySeparatorChar) ||
            modelName.Contains(Path.AltDirectorySeparatorChar))
        {
            return modelName;
        }

        if (File.Exists(modelName))
        {
            return modelName;
        }

        return SettingsPath.GetManagedModelPath(modelName);
    }
}
