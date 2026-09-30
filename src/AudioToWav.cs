using FFMpegCore;

namespace VoiceRecogniseBot;

/// <summary>
/// Converts media files to 16 kHz mono WAV using FFMpegCore.
/// </summary>
internal static class AudioToWav
{
    private static bool _ffmpegConfigured;

    public static async Task<string> ConvertToWavAsync(string inputFilePath, CancellationToken cancellationToken)
    {
        EnsureFfmpegConfigured();

        var outputFilePath = TempFiles.Create(".wav");

        try
        {
            await FFMpegArguments
                .FromFileInput(inputFilePath)
                .OutputToFile(outputFilePath, overwrite: true, options => options
                    .WithCustomArgument("-vn")
                    .WithCustomArgument("-ac 1")
                    .WithCustomArgument("-ar 16000")
                    .ForceFormat("wav"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();
        }
        catch (FFMpegCore.Exceptions.FFMpegException ex)
        {
            TempFiles.TryDelete(outputFilePath);
            throw new MediaConversionException(
                "ffmpeg failed to convert the media file. Check that the file format is supported and ffmpeg is installed correctly.",
                ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            TempFiles.TryDelete(outputFilePath);
            throw new MediaConversionException(
                "ffmpeg binary was not found. Install ffmpeg or set FFMPEG_PATH to the folder containing the ffmpeg executable.",
                ex);
        }
        catch
        {
            TempFiles.TryDelete(outputFilePath);
            throw;
        }

        if (!File.Exists(outputFilePath))
        {
            throw new MediaConversionException("ffmpeg conversion did not produce an output file.");
        }

        AppLog.Logger.Debug("Converted {0} to wav at {1}", inputFilePath, outputFilePath);
        return outputFilePath;
    }

    /// <summary>
    /// Returns the ffmpeg executable that would be used, or null when none can be found.
    /// </summary>
    public static string? FindFfmpeg()
    {
        var fileName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var configuredPath = Environment.GetEnvironmentVariable("FFMPEG_PATH");

        var folders = string.IsNullOrWhiteSpace(configuredPath)
            ? (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [Directory.Exists(configuredPath) ? configuredPath : Path.GetDirectoryName(configuredPath) ?? string.Empty];

        return folders
            .Select(folder => Path.Combine(folder, fileName))
            .FirstOrDefault(File.Exists);
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
}
