namespace VoiceRecogniseBot;

internal sealed class MediaConversionException : Exception
{
    public MediaConversionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
