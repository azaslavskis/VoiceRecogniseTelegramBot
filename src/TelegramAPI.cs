using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace VoiceRecogniseBot;

/// <summary>
/// Provides functionality to interact with the Telegram API for speech recognition.
/// </summary>
internal sealed class TelegramApi
{
    private const string StartCommand = "start";
    private const string SlashStartCommand = "/start";

    private readonly WhisperAPI _voiceRecognise = new();
    private readonly List<string> _languagesInUse;
    private readonly TelegramBotClient? _botClient;
    private readonly string _token;
    private readonly BotTextConfig _botText;
    private string _currentLanguage;

    private static readonly TelegramBotLogger AppLog = new();

    public TelegramApi()
    {
        var config = new Config().LoadAppConfig();
        _botText = config.BotText ?? new BotTextConfig();

        _currentLanguage = string.IsNullOrWhiteSpace(config.DefaultLang)
            ? "EN"
            : config.DefaultLang;

        _languagesInUse = config.Lang
            .Where(lang => !string.IsNullOrWhiteSpace(lang))
            .Select(lang => lang.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_languagesInUse.Count == 0)
        {
            _languagesInUse.Add(_currentLanguage);
        }

        _token = config.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_token) || string.Equals(_token, "xxxx", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Telegram bot token is not configured. Use config-set --token <value> first.");
            AppLog.logger.Error("Telegram bot token is missing.");
            return;
        }

        AppLog.logger.Info("Loaded Telegram configuration. Default language: {0}. Languages: {1}",
            _currentLanguage,
            string.Join(", ", _languagesInUse));

        _botClient = new TelegramBotClient(_token);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_botClient is null)
        {
            return;
        }

        var me = await _botClient.GetMe(cancellationToken);
        AppLog.logger.Info("Bot authenticated as {0} ({1})", me.Username, me.Id);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = []
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationTokenSource.Token);

        Console.WriteLine($"Listening for @{me.Username}. Press Ctrl+C to stop.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            AppLog.logger.Info("Telegram bot shutdown requested.");
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            return;
        }

        AppLog.logger.Debug("Received update {0} of type {1}", message.Date, message.Type);
        new StatsManager().IncrementMessageCount();

        await HandleMediaMessageAsync(botClient, message, cancellationToken);
        await HandleTextMessageAsync(botClient, message, cancellationToken);
    }

    private async Task HandleMediaMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        var fileId = GetTranscriptionFileId(message);
        if (fileId is null)
        {
            return;
        }

        var telegramFile = await botClient.GetFile(fileId, cancellationToken);
        var destinationFilePath = CreateDownloadPath(telegramFile.FilePath, message);
        try
        {
            AppLog.logger.Debug("Downloading media file {0} to {1}", fileId, destinationFilePath);

            await using (var fileStream = File.Create(destinationFilePath))
            {
                await botClient.GetInfoAndDownloadFile(fileId, fileStream, cancellationToken);
            }

            await botClient.SendMessage(
                message.Chat.Id,
                _botText.TranscriptionInProgressMessage,
                cancellationToken: cancellationToken);

            string? recognisedText;
            try
            {
                recognisedText = RecogniseDownloadedFile(message, destinationFilePath);
            }
            catch (MediaConversionException ex)
            {
                AppLog.logger.Error(ex, "Media conversion failed for file {0}", destinationFilePath);
                await botClient.SendMessage(
                    message.Chat.Id,
                    ex.Message,
                    cancellationToken: cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                AppLog.logger.Error(ex, "Unexpected transcription error for file {0}", destinationFilePath);
                await botClient.SendMessage(
                    message.Chat.Id,
                    _botText.InternalErrorMessage,
                    cancellationToken: cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(recognisedText))
            {
                return;
            }

            var response = $"{_botText.TranscriptionResultPrefix}\n{recognisedText}";
            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: response,
                cancellationToken: cancellationToken);
        }
        finally
        {
            TryDeleteTempFile(destinationFilePath);
        }
    }

    private string? RecogniseDownloadedFile(Message message, string destinationFilePath)
    {
        if (message.Voice is not null)
        {
            AppLog.logger.Debug("Sending voice file for transcription. Language: {0}", _currentLanguage);
            return _voiceRecognise.RecogniseVoiceFile(destinationFilePath, _currentLanguage);
        }

        if (message.Audio is not null)
        {
            AppLog.logger.Debug("Sending audio file for transcription. Language: {0}", _currentLanguage);
            return _voiceRecognise.RecogniseAudioFile(destinationFilePath, _currentLanguage);
        }

        if (message.VideoNote is not null || message.Video is not null)
        {
            AppLog.logger.Debug("Sending video file for transcription. Language: {0}", _currentLanguage);
            return _voiceRecognise.RecogniseVideoFile(destinationFilePath, _currentLanguage);
        }

        return null;
    }

    private async Task HandleTextMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var text = message.Text.Trim();
        var chatId = message.Chat.Id;

        if (TrySetLanguage(text))
        {
            await botClient.SendMessage(
                chatId,
                $"{_botText.LanguageChangedPrefix} {_currentLanguage}",
                cancellationToken: cancellationToken);
            return;
        }

        switch (text)
        {
            case StartCommand:
            case SlashStartCommand:
                await SendMainKeyboardAsync(botClient, chatId, cancellationToken);
                return;
            case var value when string.Equals(value, _botText.SetLanguageButton, StringComparison.Ordinal):
                await SendLanguageKeyboardAsync(botClient, chatId, cancellationToken);
                return;
            case var value when string.Equals(value, _botText.AboutButton, StringComparison.Ordinal):
                await botClient.SendMessage(
                    chatId,
                    _botText.AboutMessage,
                    cancellationToken: cancellationToken);
                return;
            case var value when string.Equals(value, _botText.LogButton, StringComparison.Ordinal):
                await botClient.SendMessage(
                    chatId,
                    AppLog.ReturnLogAsString(),
                    cancellationToken: cancellationToken);
                return;
            default:
                await botClient.SendMessage(
                    chatId,
                    _botText.UnknownCommandMessage,
                    cancellationToken: cancellationToken);
                return;
        }
    }

    private async Task SendMainKeyboardAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup([
            [_botText.SetLanguageButton, _botText.LogButton, _botText.AboutButton]
        ])
        {
            ResizeKeyboard = true
        };

        await botClient.SendMessage(
            chatId,
            _botText.MainMenuPrompt,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendLanguageKeyboardAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var buttons = _languagesInUse
            .Select(lang => new KeyboardButton(lang))
            .Select(button => new[] { button })
            .ToArray();

        var keyboard = new ReplyKeyboardMarkup(buttons)
        {
            ResizeKeyboard = true
        };

        await botClient.SendMessage(
            chatId,
            _botText.LanguagePrompt,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private bool TrySetLanguage(string text)
    {
        var matchedLanguage = _languagesInUse
            .FirstOrDefault(lang => string.Equals(lang, text, StringComparison.OrdinalIgnoreCase));

        if (matchedLanguage is null)
        {
            return false;
        }

        _currentLanguage = matchedLanguage;
        AppLog.logger.Info("Recognition language changed to {0}", _currentLanguage);
        return true;
    }

    private static string? GetTranscriptionFileId(Message message)
    {
        return message.Voice?.FileId
               ?? message.Audio?.FileId
               ?? message.VideoNote?.FileId
               ?? message.Video?.FileId;
    }

    private static string CreateDownloadPath(string? telegramFilePath, Message message)
    {
        var extension = GetPreferredExtension(telegramFilePath, message);
        return Path.ChangeExtension(Path.GetTempFileName(), extension);
    }

    private static string GetPreferredExtension(string? telegramFilePath, Message message)
    {
        var extensionFromTelegram = Path.GetExtension(telegramFilePath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extensionFromTelegram))
        {
            return extensionFromTelegram;
        }

        var fileNameExtension = Path.GetExtension(message.Audio?.FileName ?? message.Video?.FileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(fileNameExtension))
        {
            return fileNameExtension;
        }

        if (message.Voice is not null)
        {
            return ".ogg";
        }

        if (message.Video is not null || message.VideoNote is not null)
        {
            return ".mp4";
        }

        return ".bin";
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
        catch (Exception ex)
        {
            AppLog.logger.Warn(ex, "Could not delete temporary file {0}", path);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        AppLog.logger.Error(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}
