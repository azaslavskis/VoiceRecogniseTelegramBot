using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace VoiceRecogniseBot;

/// <summary>
/// Receives Telegram updates and replies with transcriptions of voice, audio and video messages.
/// </summary>
internal sealed class TelegramApi : IDisposable
{
    private const string StartCommand = "start";
    private const string SlashStartCommand = "/start";
    private const int MaxMessageLength = 4096;
    private const int LogLinesInChat = 30;

    private static readonly TimeSpan ConfigPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly WhisperApi _whisper = new();
    private readonly ConcurrentDictionary<long, string> _chatLanguages = new();

    /// <summary>
    /// Runs the bot until cancelled. The token is re-read from the configuration, so setting or
    /// changing it (for example from the web UI) starts or reconnects the bot without a restart.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var token = ConfigStore.Load().Token;
                if (!ConfigStore.IsTokenConfigured(token))
                {
                    BotStatus.Set(BotState.WaitingForToken);
                    AppLog.Logger.Warn("Telegram bot token is not configured. Set it in the web UI or with 'config-set --token <value>'.");
                    await WaitForTokenChangeAsync(token, Timeout.InfiniteTimeSpan, cancellationToken);
                    continue;
                }

                await RunSessionAsync(token, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AppLog.Logger.Info("Telegram bot shutdown requested.");
        }
        finally
        {
            BotStatus.Set(BotState.Stopped);
        }
    }

    public void Dispose() => _whisper.Dispose();

    /// <summary>
    /// Connects with one token and receives updates until the token changes or shutdown is requested.
    /// </summary>
    private async Task RunSessionAsync(string token, CancellationToken cancellationToken)
    {
        BotStatus.Set(BotState.Connecting);

        TelegramBotClient botClient;
        User me;
        try
        {
            botClient = new TelegramBotClient(token);
            me = await botClient.GetMe(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A rejected token stays rejected, a network failure may recover: retry after a delay
            // unless the token is changed first.
            AppLog.Logger.Error("Could not connect to Telegram: {0}", ex.Message);
            AppLog.Logger.Debug(ex, "Connection failure details");
            BotStatus.Set(BotState.Error, error: ex.Message);
            await WaitForTokenChangeAsync(token, RetryDelay, cancellationToken);
            return;
        }

        using var sessionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = [] },
            cancellationToken: sessionTokenSource.Token);

        BotStatus.Set(BotState.Online, username: me.Username);
        AppLog.Logger.Info("Listening as @{0} ({1})", me.Username, me.Id);

        try
        {
            await WaitForTokenChangeAsync(token, Timeout.InfiniteTimeSpan, cancellationToken);
            AppLog.Logger.Info("Telegram token changed; reconnecting.");
        }
        finally
        {
            await sessionTokenSource.CancelAsync();
        }
    }

    /// <summary>
    /// Completes when the configured token differs from <paramref name="currentToken"/> or the timeout elapses.
    /// </summary>
    private static async Task WaitForTokenChangeAsync(string currentToken, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = timeout == Timeout.InfiniteTimeSpan ? DateTime.MaxValue : DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(ConfigPollInterval, cancellationToken);
            if (!string.Equals(ConfigStore.Load().Token, currentToken, StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
        {
            return;
        }

        AppLog.Logger.Debug("Received message {0} of type {1}", message.Id, message.Type);
        StatsStore.RecordMessage();

        var config = ConfigStore.Load();
        try
        {
            if (GetTranscriptionFileId(message) is { } fileId)
            {
                await HandleMediaMessageAsync(botClient, message, fileId, config, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(message.Text))
            {
                await HandleTextMessageAsync(botClient, message.Chat.Id, message.Text.Trim(), config, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One failing message must not take the receive loop down with it.
            AppLog.Logger.Error(ex, "Failed to handle message {0}", message.Id);
        }
    }

    private async Task HandleMediaMessageAsync(
        ITelegramBotClient botClient,
        Message message,
        string fileId,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var language = GetChatLanguage(chatId, config);
        var telegramFile = await botClient.GetFile(fileId, cancellationToken);
        var downloadPath = TempFiles.Create(GetPreferredExtension(telegramFile.FilePath, message));

        try
        {
            AppLog.Logger.Debug("Downloading media file {0} to {1}", fileId, downloadPath);
            await using (var fileStream = File.Create(downloadPath))
            {
                await botClient.DownloadFile(telegramFile, fileStream, cancellationToken);
            }

            await botClient.SendMessage(chatId, config.BotText.TranscriptionInProgressMessage, cancellationToken: cancellationToken);

            string recognisedText;
            try
            {
                recognisedText = await _whisper.TranscribeAsync(downloadPath, config.Model, language, cancellationToken);
            }
            catch (MediaConversionException ex)
            {
                AppLog.Logger.Error(ex, "Media conversion failed for file {0}", downloadPath);
                await botClient.SendMessage(chatId, ex.Message, cancellationToken: cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Logger.Error(ex, "Unexpected transcription error for file {0}", downloadPath);
                await botClient.SendMessage(chatId, config.BotText.InternalErrorMessage, cancellationToken: cancellationToken);
                return;
            }

            StatsStore.RecordTranscription();

            if (!string.IsNullOrWhiteSpace(recognisedText))
            {
                await SendLongMessageAsync(botClient, chatId, $"{config.BotText.TranscriptionResultPrefix}\n{recognisedText}", cancellationToken);
            }
        }
        finally
        {
            TempFiles.TryDelete(downloadPath);
        }
    }

    private async Task HandleTextMessageAsync(
        ITelegramBotClient botClient,
        long chatId,
        string text,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        var botText = config.BotText;

        var requestedLanguage = config.Lang.FirstOrDefault(lang => string.Equals(lang, text, StringComparison.OrdinalIgnoreCase));
        if (requestedLanguage is not null)
        {
            _chatLanguages[chatId] = requestedLanguage;
            AppLog.Logger.Info("Recognition language for chat {0} changed to {1}", chatId, requestedLanguage);
            await botClient.SendMessage(chatId, $"{botText.LanguageChangedPrefix} {requestedLanguage}", cancellationToken: cancellationToken);
            return;
        }

        if (text is StartCommand or SlashStartCommand)
        {
            var keyboard = new ReplyKeyboardMarkup([[botText.SetLanguageButton, botText.LogButton, botText.AboutButton]])
            {
                ResizeKeyboard = true
            };
            await botClient.SendMessage(chatId, botText.MainMenuPrompt, replyMarkup: keyboard, cancellationToken: cancellationToken);
        }
        else if (text == botText.SetLanguageButton)
        {
            var keyboard = new ReplyKeyboardMarkup(config.Lang.Select(lang => new[] { new KeyboardButton(lang) }))
            {
                ResizeKeyboard = true
            };
            await botClient.SendMessage(chatId, botText.LanguagePrompt, replyMarkup: keyboard, cancellationToken: cancellationToken);
        }
        else if (text == botText.AboutButton)
        {
            await botClient.SendMessage(chatId, botText.AboutMessage, cancellationToken: cancellationToken);
        }
        else if (text == botText.LogButton)
        {
            var lines = AppLog.ReadTail(LogLinesInChat);
            var log = lines.Count == 0 ? "No log messages yet." : string.Join('\n', lines);

            // Keep the most recent part when the tail does not fit into one message.
            await botClient.SendMessage(chatId, log.Length > MaxMessageLength ? log[^MaxMessageLength..] : log, cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendMessage(chatId, botText.UnknownCommandMessage, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Returns the language chosen in this chat, falling back to the default when none was chosen
    /// or the choice has since been removed from the configuration.
    /// </summary>
    private string GetChatLanguage(long chatId, AppConfig config)
    {
        return _chatLanguages.TryGetValue(chatId, out var language) && config.Lang.Contains(language)
            ? language
            : config.DefaultLang;
    }

    /// <summary>
    /// Telegram rejects messages above 4096 characters, so long transcriptions are sent in parts,
    /// split on line breaks where possible.
    /// </summary>
    private static async Task SendLongMessageAsync(ITelegramBotClient botClient, long chatId, string text, CancellationToken cancellationToken)
    {
        var remaining = text.AsMemory().TrimEnd();

        while (remaining.Length > 0)
        {
            var length = Math.Min(remaining.Length, MaxMessageLength);
            if (length < remaining.Length)
            {
                var lineBreak = remaining.Span[..length].LastIndexOf('\n');
                if (lineBreak > 0)
                {
                    length = lineBreak;
                }
            }

            await botClient.SendMessage(chatId, remaining[..length].ToString(), cancellationToken: cancellationToken);
            remaining = remaining[length..].TrimStart();
        }
    }

    private static string? GetTranscriptionFileId(Message message)
    {
        return message.Voice?.FileId
               ?? message.Audio?.FileId
               ?? message.VideoNote?.FileId
               ?? message.Video?.FileId;
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

        return message.Video is not null || message.VideoNote is not null ? ".mp4" : ".bin";
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        AppLog.Logger.Error(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}

internal enum BotState
{
    /// <summary>The bot does not run in this process (web UI only).</summary>
    NotRunning,
    WaitingForToken,
    Connecting,
    Online,
    Error,
    Stopped
}

/// <summary>
/// Connection state of the bot in this process, reported by the web UI.
/// </summary>
internal sealed record BotStatus(BotState State, string? Username = null, string? Error = null)
{
    public static BotStatus Current { get; private set; } = new(BotState.NotRunning);

    public static void Set(BotState state, string? username = null, string? error = null)
    {
        Current = new BotStatus(state, username, error);
    }
}
