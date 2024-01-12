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
        public static string currentlang = "eng";
        private List<string> langs_in_use = new List<string>();
        private WhisperAPI VoiceRecognise = new WhisperAPI();

        /// <summary>
        /// Initializes a new instance of the <see cref="TelegramAPI"/> class.
        /// </summary>
        public TelegramAPI()
        {
            var config = new Config();
            var result = config.GetConfig().GetSection("lang").AsEnumerable();

            if (result.Any())
            {
                foreach (var item in result)
                {
                    if (item.Value != null) langs_in_use.Add(item.Value);
                }
            }

            Console.WriteLine(langs_in_use.Count);

            var botClient = new TelegramBotClient(config.GetConfig()["token"]);

            var me = botClient.GetMeAsync().Result;
            Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

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

            Console.WriteLine($"Start listening for @{me.Username}");

            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();
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
            if (update.Message is  { } message)
            {

                Console.WriteLine($"Update message: {update.Message.Type}");

                if (update.Message.Voice != null)
                {
                    Console.WriteLine("voice api");
                    Console.WriteLine(message.Voice.Duration);
                    Console.WriteLine(message.Voice.FileId);
                    string destinationFilePath = Path.GetTempFileName();

                    await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                    var file = await botClient.GetInfoAndDownloadFileAsync(
                        fileId: message.Voice.FileId,
                        destination: fileStream,
                        cancellationToken: cancellationToken);


                    fileStream.Close();

                    if ( destinationFilePath != null && fileStream.CanWrite == false)
                    {
                        //currentlang
                        var text  = VoiceRecognise.RecogniseWav(destinationFilePath, currentlang);
                        var message_return  = string.Format($"Сообщение распознанно! Содержимое \n {text}");
                        await botClient.SendTextMessageAsync(
                         chatId: update.Message.Chat.Id,
                         text: message_return,
                         cancellationToken: cancellationToken);
                    }
                }
                if (update.Message.VideoNote != null)
                {
                    string destinationFilePath = Path.GetTempFileName();

                    await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                    var file = await botClient.GetInfoAndDownloadFileAsync(
                        fileId: message.VideoNote.FileId,
                        destination: fileStream,
                        cancellationToken: cancellationToken);
                    fileStream.Close();

                    if (destinationFilePath != null && fileStream.CanWrite == false)
                    {
                        //currentlang
                        var text = VoiceRecognise.RecogniseMp4(destinationFilePath, currentlang);
                        var message_return = string.Format($"Сообщение распознанно! Содержимое \n {text}");
                        await botClient.SendTextMessageAsync(
                         chatId: update.Message.Chat.Id,
                         text: message_return,
                         cancellationToken: cancellationToken);
                    }

                }
                if (update.Message.Audio != null)
                {
                    string destinationFilePath = Path.GetTempFileName();

                    await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                    var file = await botClient.GetInfoAndDownloadFileAsync(
                        fileId: message.Audio.FileId,
                        destination: fileStream,
                        cancellationToken: cancellationToken);

                    fileStream.Close();
                    var text = VoiceRecognise.RecogniseWav(destinationFilePath, currentlang);
                    var message_return = string.Format($"Сообщение распознанно! Содержимое \n {text}");
                    await botClient.SendTextMessageAsync(
                     chatId: update.Message.Chat.Id,
                     text: message_return,
                     cancellationToken: cancellationToken);
                }

                if (update.Message.Video != null)
                {
                    string destinationFilePath = Path.GetTempFileName();

                    await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                    var file = await botClient.GetInfoAndDownloadFileAsync(
                        fileId: message.Video.FileId,
                        destination: fileStream,
                        cancellationToken: cancellationToken);

                    fileStream.Close();
                    var text = VoiceRecognise.RecogniseMp4(destinationFilePath, currentlang);
                    var message_return = string.Format($"Сообщение распознанно! Содержимое \n {text}");
                    await botClient.SendTextMessageAsync(
                     chatId: update.Message.Chat.Id,
                     text: message_return,
                     cancellationToken: cancellationToken);
                }
            }
            var msg = update.Message;
            var chatId = update.Message.Chat.Id;
            //Console.WriteLine(message.Text);
            if (msg.Text != null)
            {
                switch (msg.Text)
                {
                    case "start":
                        Console.WriteLine("here");
                        ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
            {
    new KeyboardButton[] { "Set Lang","Info","About" },
})
                        {
                            ResizeKeyboard = true
                        };

                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Choose a response",
                            replyMarkup: replyKeyboardMarkup,
                            cancellationToken: cancellationToken);
                        break;
                    case "Set Lang":
                        KeyboardButton[] array = new KeyboardButton[langs_in_use.Count];
                        int i = 0;
                        foreach (var lang in langs_in_use)
                        {
                            array[i] = new KeyboardButton(lang);
                            i++;
                        }
                        ReplyKeyboardMarkup langreplyKeyboard = new ReplyKeyboardMarkup(array)
                        {
                            ResizeKeyboard = true        
                        };
                        Message langsentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Choose a response",
                            replyMarkup: langreplyKeyboard,
                            cancellationToken: cancellationToken);
                        break;
                    case "About":
                        Message aboutmessage = await botClient.SendTextMessageAsync(
                          chatId: chatId,
                          text: "bot message",
                          cancellationToken: cancellationToken);
                        break;
                    case "log":
                         await botClient.SendTextMessageAsync(
                          chatId: chatId,
                          text: "bot message",
                          cancellationToken: cancellationToken);
                        break;
                }

                bool containsElement = langs_in_use.Any(element => update.Message.Text.Contains(element));
                if (containsElement)
                {
                    currentlang = update.Message.Text;
                    var msg_value = string.Format($"Changed message recognition language to {currentlang}");

                    await botClient.SendTextMessageAsync(
                          chatId: chatId,
                          text: msg_value,
                          cancellationToken: cancellationToken);

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
