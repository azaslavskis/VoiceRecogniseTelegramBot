using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace VoiceRecogniseBot
{
    /// <summary>
    /// Provides functionality to interact with the Telegram API for speech recognition.
    /// </summary>
    internal class TelegramAPI
    {
        public static string currentlang = "";
        private List<string> langs_in_use = new List<string>();
        private WhisperAPI VoiceRecognise = new WhisperAPI();
        private static TelegramBotLogger app_log = new TelegramBotLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="TelegramAPI"/> class.
        /// </summary>
        public TelegramAPI()
        {

            var config = new Config();
            if (config.GetConfig()["default_lang"] != null) currentlang = config.GetConfig()["default_lang"];
            app_log.logger.Info($"Getting default lang from config {currentlang}");

            var result = config.GetConfig().GetSection("lang").AsEnumerable();

            if (result.Any())
            {
                foreach (var item in result)
                {
                    if (item.Value != null) langs_in_use.Add(item.Value);
                }
            }

            app_log.logger.Info($"Parsing lang from config is fine {langs_in_use} count {langs_in_use.Count}");

            if (config.GetConfig()["token"] != null)
            {
                app_log.logger.Debug($"Token is not null");
                var botClient = new TelegramBotClient(config.GetConfig()["token"]);

                var me = botClient.GetMeAsync().Result;
                app_log.logger.Debug($"Recognised as {me.FirstName} and {me.Id}");

                using CancellationTokenSource cts = new();

                // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
                ReceiverOptions receiverOptions = new()
                {
                    AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
                };

                botClient.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    pollingErrorHandler: HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: cts.Token
                );

          
                app_log.logger.Debug($"Start listening for @{me.Username}");

                Console.ReadLine();

                // Send cancellation request to stop bot
                cts.Cancel();
            }
            else
            {
                Console.WriteLine("Token is null! Check config");
                return;
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

                app_log.logger.Debug($"New message update {message.Date } {message.Type}");

                if (update.Message != null)
                {
                    string text = null;
                    string destinationFilePath = Path.GetTempFileName();

                    if (update.Message.Voice != null || update.Message.Audio != null)
                    {
                        app_log.logger.Debug($"New message update {message.Date} {message.Type}");
                        string fileId = update.Message.Voice?.FileId ?? update.Message.Audio?.FileId;
                        app_log.logger.Debug($"File id:{fileId}");
                        await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                        var file = await botClient.GetInfoAndDownloadFileAsync(fileId, fileStream, cancellationToken);
                        app_log.logger.Debug($"File path:{destinationFilePath}");
                        fileStream.Close();

                        if (fileStream.CanWrite == false)
                        {
                            text = VoiceRecognise.RecogniseWav(destinationFilePath, currentlang);
                            app_log.logger.Debug($"Getting text from Whisper {text}");
                        }
                    }
                    else if (update.Message.VideoNote != null || update.Message.Video != null)
                    {
                        app_log.logger.Debug($"New message update {message.Date} {message.Type}");
                        string fileId = update.Message.VideoNote?.FileId ?? update.Message.Video?.FileId;
                        await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                        app_log.logger.Debug($"File id:{fileId}");
                        var file = await botClient.GetInfoAndDownloadFileAsync(fileId, fileStream, cancellationToken);
                        app_log.logger.Debug($"File path:{destinationFilePath}");
                        fileStream.Close();

                        if (fileStream.CanWrite == false)
                        {
                            
                            text = VoiceRecognise.RecogniseMp4(destinationFilePath, currentlang);
                            app_log.logger.Debug($"Getting text from Whisper {text}");
                        }
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                    
                        var message_return = string.Format($"Сообщение распознано! Содержимое \n {text}");
                        app_log.logger.Debug($"send message! todo:custom message from config {message_return}");
                        await botClient.SendTextMessageAsync(
                         chatId: update.Message.Chat.Id,
                         text: message_return,
                         cancellationToken: cancellationToken);
                        
                    }
                }
            }


            var msg = update.Message;
            var chatId = msg.Chat.Id;
            app_log.logger.Debug($"update {msg} chat_id:{chatId}");
            if (msg.Text != null)
            {
                Dictionary<string, Func<Task>> commandActions = new Dictionary<string, Func<Task>>
    {
        { "start", async () =>
            {
                Console.WriteLine("here");
                var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Set Lang", "Log", "About" },
                })
                {
                    ResizeKeyboard = true
                };
                 app_log.logger.Debug($"update {msg} chat_id:{chatId} send keyboard");

                await botClient.SendTextMessageAsync(chatId, "Choose a response", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
            }
        },
        { "Set Lang", async () =>
            {
                var langButtons = langs_in_use.Select(lang => new KeyboardButton(lang)).ToArray();
                var langReplyKeyboard = new ReplyKeyboardMarkup(langButtons) { ResizeKeyboard = true };
                    app_log.logger.Debug($"update {msg} chat_id:{chatId} send lang");
                await botClient.SendTextMessageAsync(chatId, "Choose a response", replyMarkup: langReplyKeyboard, cancellationToken: cancellationToken);
            }
        },
        { "About", async () =>
            {
                  app_log.logger.Debug($"update {msg} chat_id:{chatId} send about message");
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

                bool containsElement = langs_in_use.Contains(msg.Text);
                if (containsElement)
                {
                    currentlang = msg.Text;
                    var msg_value = $"Changed message recognition language to {currentlang}";
                    app_log.logger.Debug($"update {msg} chat_id:{chatId} lang change to {currentlang}");
                    await botClient.SendTextMessageAsync(chatId, msg_value, cancellationToken: cancellationToken);
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
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

    }
}
