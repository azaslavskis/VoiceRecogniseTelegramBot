// See https://aka.ms/new-console-template for more information\

namespace VoiceRecogniseBot;

public class AppConfig
{
    public required string Model { get; set; }
    public required string Token { get; set; }
    public required List<string> Lang { get; set; }
    public required string DefaultLang { get; set; }
}