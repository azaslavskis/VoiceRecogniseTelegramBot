using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace VoiceRecogniseBot
{
    internal class TelegramAPI
        
    {
        public static string currentlang = "eng";
        private string[]  langs_in_use = { "LV", "RU", "ENG" };
        private WhisperAPI VoiceRecognise = new WhisperAPI();
        public TelegramAPI()
        {

            var config = new Config();
            

            var botClient = new TelegramBotClient(config.GetConfig()["token"]); ;

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

                }

                if (update.Message.Audio != null)
                {
                  
                }


                //return;
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


                        KeyboardButton[] array = new KeyboardButton[langs_in_use.Length];
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
