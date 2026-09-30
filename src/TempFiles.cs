namespace VoiceRecogniseBot;

internal static class TempFiles
{
    /// <summary>
    /// Returns a unique path in the temp directory; the file itself is not created.
    /// </summary>
    public static string Create(string extension)
    {
        return Path.Combine(Path.GetTempPath(), $"voicebot-{Guid.NewGuid():N}{extension}");
    }

    public static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Logger.Warn(ex, "Could not delete temporary file {0}", path);
        }
    }
}
