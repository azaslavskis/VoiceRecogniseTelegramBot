using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace VoiceRecogniseBot
{
    /// <summary>
    /// Provides functionality to interact with the Telegram API for speech recognition.
    /// </summary>
    internal class TelegramApi
    {
        private static string? _currentLang = "";
        private readonly List<string> _langsInUse = new List<string>();
        private readonly WhisperAPI _voiceRecognise = new WhisperAPI();
        private static readonly TelegramBotLogger AppLog = new TelegramBotLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="TelegramApi"/> class.
        /// </summary>
        public TelegramApi()
        {

            var config = new Config();
            if (config.GetConfig()["default_lang"] != null) _currentLang = config.GetConfig()["default_lang"];
            AppLog.logger.Info($"Getting default lang from config {_currentLang}");

            var result = config.GetConfig().GetSection("lang").AsEnumerable();

            var keyValuePairs = result as KeyValuePair<string, string?>[] ?? result.ToArray();
            if (keyValuePairs.Any())
            {
                foreach (var item in keyValuePairs)
                {
                    if (item.Value != null) _langsInUse.Add(item.Value);
                }
            }

            AppLog.logger.Info($"Parsing lang from config is fine {_langsInUse} count {_langsInUse.Count}");

            if (config.GetConfig()["token"] != null)
            {
                AppLog.logger.Debug($"Token is not null");
                var botClient = new TelegramBotClient(config.GetConfig()["token"] ?? string.Empty);

                var me = botClient.GetMeAsync().Result;
                AppLog.logger.Debug($"Recognised as {me.FirstName} and {me.Id}");

                using CancellationTokenSource cts = new();

                // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
                ReceiverOptions receiverOptions = new()
                {
                    AllowedUpdates = [] // receive all update types except ChatMember related updates
                };

                botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    pollingErrorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cts.Token
                );

          
                AppLog.logger.Debug($"Start listening for @{me.Username}");

                Console.ReadLine();

                // Send cancellation request to stop bot
                cts.Cancel();
            }
            else
            {
                Console.WriteLine("Token is null! Check config");
            }
        }

        // HandleUpdateAsync method and other methods here...



        /// <summary>
        /// Handles incoming updates from the Telegram bot.
        /// </summary>
        /// <param name="botClient">The Telegram bot client.</param>
        /// <param name="update">The incoming update.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {

            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Message is { } message)
            {

                AppLog.logger.Debug($"New message update {message.Date } {message.Type}");

                if (update.Message != null)
                {
                    string? text = null;
                    string destinationFilePath = Path.GetTempFileName();

                    if (update.Message.Voice != null || update.Message.Audio != null)
                    {
                    
                        AppLog.logger.Debug($"New message update {message.Date} {message.Type}");
                        var fileId = update.Message.Voice?.FileId ?? update.Message.Audio?.FileId;
                        AppLog.logger.Debug($"File id:{fileId}");
                        await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                        if (fileId != null)
                        {
                            await botClient.GetInfoAndDownloadFileAsync(fileId, fileStream,
                                cancellationToken);
                        }

                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "In progress", cancellationToken: cancellationToken);
                        AppLog.logger.Debug($"File path:{destinationFilePath}");
                        fileStream.Close();

                        if (fileStream.CanWrite == false)
                        {
                            text = _voiceRecognise.RecogniseWav(destinationFilePath, _currentLang);
                            AppLog.logger.Debug($"Getting text from Whisper {text}");
                        }
                    }
                    else if (update.Message.VideoNote != null || update.Message.Video != null)
                    {
                        AppLog.logger.Debug($"New message update {message.Date} {message.Type}");
                        string? fileId = update.Message.VideoNote?.FileId ?? update.Message.Video?.FileId;
                        await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                        AppLog.logger.Debug($"File id:{fileId}");
                        if (fileId != null)
                        {
                            await botClient.GetInfoAndDownloadFileAsync(fileId, fileStream,
                                cancellationToken);
                        }

                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "In progress", cancellationToken: cancellationToken);
                        AppLog.logger.Debug($"File path:{destinationFilePath}");
                        fileStream.Close();

                        if (fileStream.CanWrite == false)
                        {
                            
                            text = _voiceRecognise.RecogniseMp4(destinationFilePath, _currentLang);
                            AppLog.logger.Debug($"Getting text from Whisper {text}");
                        }
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                    
                        var messageReturn = string.Format($"Сообщение распознано! Содержимое \n {text}");
                        AppLog.logger.Debug($"send message! todo:custom message from config {messageReturn}");
                        await botClient.SendTextMessageAsync(
                         chatId: update.Message.Chat.Id,
                         text: messageReturn,
                         cancellationToken: cancellationToken);
                        
                    }
                }
            }


            var msg = update.Message;
            if (msg != null)
            {
                var chatId = msg.Chat.Id;
                AppLog.logger.Debug($"update {msg} chat_id:{chatId}");
                if (msg.Text != null)
                {
                    Dictionary<string, Func<Task>> commandActions = new Dictionary<string, Func<Task>>
                    {
                        { "start", async () =>
                            {
                                Console.WriteLine("here");
                                var replyKeyboardMarkup = new ReplyKeyboardMarkup([
                                    ["Set Lang", "Log", "About"]
                                ])
                                {
                                    ResizeKeyboard = true
                                };
                                AppLog.logger.Debug($"update {msg} chat_id:{chatId} send keyboard");

                                await botClient.SendTextMessageAsync(chatId, "Choose a response", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
                            }
                        },
                        { "Set Lang", async () =>
                            {
                                var langButtons = _langsInUse.Select(lang => new KeyboardButton(lang)).ToArray();
                                var langReplyKeyboard = new ReplyKeyboardMarkup(langButtons) { ResizeKeyboard = true };
                                AppLog.logger.Debug($"update {msg} chat_id:{chatId} send lang");
                                await botClient.SendTextMessageAsync(chatId, "Choose a response", replyMarkup: langReplyKeyboard, cancellationToken: cancellationToken);
                            }
                        },
                        { "About", async () =>
                            {
                                AppLog.logger.Debug($"update {msg} chat_id:{chatId} send about message");
                                await botClient.SendTextMessageAsync(chatId, "bot message", cancellationToken: cancellationToken);

                            }
                        },
                        { "log", async () =>
                            {

                                await botClient.SendTextMessageAsync(chatId, "bot message", cancellationToken: cancellationToken);
                            }
                        }
                    };

                    if (commandActions.ContainsKey(msg.Text))
                    {
                        await commandActions[msg.Text]();
                    }

                    bool containsElement = _langsInUse.Contains(msg.Text);
                    if (containsElement)
                    {
                        _currentLang = msg.Text;
                        var msgValue = $"Changed message recognition language to {_currentLang}";
                        AppLog.logger.Debug($"update {msg} chat_id:{chatId} lang change to {_currentLang}");
                        await botClient.SendTextMessageAsync(chatId, msgValue, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        /// <summary>
        /// Handles polling errors that occur during communication with the Telegram bot API.
        /// </summary>
        /// <param name="botClient">The Telegram bot client.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A completed task.</returns>
        Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            if (errorMessage == null) throw new ArgumentNullException(nameof(errorMessage));

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }

    }
}
