namespace VoiceRecogniseBot;

public sealed class AppConfig
{
    public string Model { get; set; } = "ggml-base";
    public string Token { get; set; } = "xxxx";
    public bool WebServer { get; set; } = true;
    public List<string> Lang { get; set; } = ["RU", "LV", "EN"];
    public string DefaultLang { get; set; } = "EN";
    public BotTextConfig BotText { get; set; } = new();
}

public sealed class BotTextConfig
{
    public string SetLanguageButton { get; set; } = "Set Lang";
    public string LogButton { get; set; } = "Log";
    public string AboutButton { get; set; } = "About";
    public string MainMenuPrompt { get; set; } = "Choose an action:";
    public string LanguagePrompt { get; set; } = "Choose the recognition language:";
    public string AboutMessage { get; set; } = "This bot transcribes Telegram voice and audio messages into text.";
    public string UnknownCommandMessage { get; set; } = "Unknown command. Send 'start' to open the bot menu.";
    public string TranscriptionInProgressMessage { get; set; } = "Transcription in progress...";
    public string TranscriptionResultPrefix { get; set; } = "Recognised message:";
    public string LanguageChangedPrefix { get; set; } = "Changed message recognition language to";
    public string InternalErrorMessage { get; set; } = "Transcription failed due to an internal error. Check the bot logs for details.";
}
