using FFMpegCore;

namespace VoiceRecogniseBot;

/// <summary>
/// Converts media files to 16 kHz mono WAV using FFMpegCore.
/// </summary>
internal sealed class AudioToWav
{
    private static readonly TelegramBotLogger AppLog = new();
    private static bool _ffmpegConfigured;

    public string ConvertToWav(string inputFilePath)
    {
        EnsureFfmpegConfigured();

        var outputFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".wav");

        try
        {
            FFMpegArguments
                .FromFileInput(inputFilePath)
                .OutputToFile(outputFilePath, overwrite: true, options => options
                    .WithCustomArgument("-vn")
                    .WithCustomArgument("-ac 1")
                    .WithCustomArgument("-ar 16000")
                    .ForceFormat("wav"))
                .ProcessSynchronously();
        }
        catch (FFMpegCore.Exceptions.FFMpegException ex)
        {
            TryDeleteTempFile(outputFilePath);
            throw new MediaConversionException(
                "ffmpeg failed to convert the media file. Check that the file format is supported and ffmpeg is installed correctly.",
                ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            TryDeleteTempFile(outputFilePath);
            throw new MediaConversionException(
                "ffmpeg binary was not found. Install ffmpeg or set FFMPEG_PATH to the folder containing the ffmpeg executable.",
                ex);
        }
        catch
        {
            TryDeleteTempFile(outputFilePath);
            throw;
        }

        if (!File.Exists(outputFilePath))
        {
            throw new InvalidOperationException("ffmpeg conversion did not produce an output file.");
        }

        AppLog.logger.Debug("Converted {0} to wav via FFMpegCore at {1}", inputFilePath, outputFilePath);
        return outputFilePath;
    }

    private static void EnsureFfmpegConfigured()
    {
        if (_ffmpegConfigured)
        {
            return;
        }

        var configuredPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var binaryFolder = Directory.Exists(configuredPath)
                ? configuredPath
                : Path.GetDirectoryName(configuredPath);

            if (!string.IsNullOrWhiteSpace(binaryFolder))
            {
                GlobalFFOptions.Configure(options => options.BinaryFolder = binaryFolder);
            }
        }

        _ffmpegConfigured = true;
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
}
