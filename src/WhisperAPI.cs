using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceRecogniseBot;

/// <summary>
/// Transcribes media files with a locally loaded Whisper model.
/// </summary>
internal sealed class WhisperApi : IDisposable
{
    /// <summary>
    /// Names of the models that can be downloaded, for example "ggml-base" or "ggml-large-v3-turbo".
    /// </summary>
    public static IReadOnlyList<string> DownloadableModels { get; } = Enum.GetNames<GgmlType>().Select(ToModelName).ToList();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModel;

    /// <summary>
    /// Converts the media file to WAV and returns the transcription, one line per segment.
    /// </summary>
    public async Task<string> TranscribeAsync(string filePath, string model, string language, CancellationToken cancellationToken)
    {
        // Whisper is CPU and memory heavy, so only one transcription runs at a time.
        await _gate.WaitAsync(cancellationToken);
        string? wavPath = null;
        try
        {
            wavPath = await AudioToWav.ConvertToWavAsync(filePath, cancellationToken);

            var factory = await GetFactoryAsync(model, cancellationToken);
            var whisperLanguage = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
            AppLog.Logger.Debug("Transcribing {0} with language {1}", filePath, whisperLanguage);

            await using var processor = factory.CreateBuilder()
                .WithLanguage(whisperLanguage)
                .Build();

            await using var wavStream = File.OpenRead(wavPath);
            var builder = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(wavStream, cancellationToken))
            {
                builder.AppendLine($"{segment.Start:hh\\:mm\\:ss}->{segment.End:hh\\:mm\\:ss}: {segment.Text.Trim()}");
            }

            AppLog.Logger.Info("Transcription completed for {0}", filePath);
            return builder.ToString();
        }
        finally
        {
            _gate.Release();
            TempFiles.TryDelete(wavPath);
        }
    }

    /// <summary>
    /// Returns a user-facing error when the model setting cannot be used, otherwise null.
    /// </summary>
    public static string? DescribeModelProblem(string model)
    {
        if (IsFilePath(model))
        {
            return File.Exists(model) ? null : $"Model file '{model}' does not exist.";
        }

        return TryParseModel(model, out _) || File.Exists(AppPaths.GetManagedModelPath(model))
            ? null
            : $"Unknown Whisper model '{model}'. Use a name such as ggml-base or ggml-large-v3-turbo, or a path to a model file.";
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _gate.Dispose();
    }

    private async Task<WhisperFactory> GetFactoryAsync(string model, CancellationToken cancellationToken)
    {
        if (_factory is not null && string.Equals(_loadedModel, model, StringComparison.Ordinal))
        {
            return _factory;
        }

        var modelPath = GetModelPath(model);
        if (!File.Exists(modelPath))
        {
            await DownloadModelAsync(model, cancellationToken);
        }

        AppLog.Logger.Info("Loading Whisper model from {0}", modelPath);
        var factory = WhisperFactory.FromPath(modelPath);

        _factory?.Dispose();
        _factory = factory;
        _loadedModel = model;
        return factory;
    }

    public static string GetModelPath(string model)
    {
        return IsFilePath(model) ? model : AppPaths.GetManagedModelPath(model);
    }

    /// <summary>
    /// Names of the model files already present in the models directory.
    /// </summary>
    public static IEnumerable<string> GetLocalModels()
    {
        return Directory.Exists(AppPaths.ModelsDirectory)
            ? Directory.EnumerateFiles(AppPaths.ModelsDirectory, "*.bin").Select(path => Path.GetFileNameWithoutExtension(path)!).Order()
            : [];
    }

    public static async Task DownloadModelAsync(string model, CancellationToken cancellationToken)
    {
        var modelPath = GetModelPath(model);
        if (IsFilePath(model) || !TryParseModel(model, out var ggmlType))
        {
            throw new InvalidOperationException(DescribeModelProblem(model) ?? $"Whisper model '{model}' is not available.");
        }

        AppLog.Logger.Info("Downloading Whisper model {0} to {1}", model, modelPath);
        Directory.CreateDirectory(AppPaths.ModelsDirectory);

        // Download to a temporary name so an interrupted download is never mistaken for a model.
        var partialPath = modelPath + ".part";
        try
        {
            await using (var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType, cancellationToken: cancellationToken))
            await using (var fileStream = File.Create(partialPath))
            {
                await modelStream.CopyToAsync(fileStream, cancellationToken);
            }

            File.Move(partialPath, modelPath, overwrite: true);
        }
        catch
        {
            TempFiles.TryDelete(partialPath);
            throw;
        }
    }

    /// <summary>
    /// Turns an enum name such as "LargeV3Turbo" or "TinyEn" into "ggml-large-v3-turbo" or "ggml-tiny.en".
    /// </summary>
    private static string ToModelName(string ggmlTypeName)
    {
        var englishOnly = ggmlTypeName.EndsWith("En", StringComparison.Ordinal);
        var baseName = englishOnly ? ggmlTypeName[..^2] : ggmlTypeName;
        var words = string.Concat(baseName.Select((c, index) => char.IsUpper(c) && index > 0 ? $"-{c}" : c.ToString()));

        return $"ggml-{words.ToLowerInvariant()}{(englishOnly ? ".en" : string.Empty)}";
    }

    private static bool IsFilePath(string model)
    {
        return Path.IsPathRooted(model) ||
               model.Contains(Path.DirectorySeparatorChar) ||
               model.Contains(Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// Maps names such as "ggml-large-v3-turbo", "base.en" or "LargeV3Turbo" to a downloadable model.
    /// </summary>
    private static bool TryParseModel(string model, out GgmlType ggmlType)
    {
        var name = model.Trim().ToLowerInvariant();
        if (name.EndsWith(".bin", StringComparison.Ordinal))
        {
            name = name[..^4];
        }

        if (name.StartsWith("ggml-", StringComparison.Ordinal))
        {
            name = name[5..];
        }

        name = name.Replace(".", string.Empty).Replace("-", string.Empty);

        // Enum.TryParse also accepts numeric strings, which are not valid model names.
        ggmlType = default;
        return name.Length > 0 &&
               !char.IsDigit(name[0]) &&
               Enum.TryParse(name, ignoreCase: true, out ggmlType) &&
               Enum.IsDefined(ggmlType);
    }
}
