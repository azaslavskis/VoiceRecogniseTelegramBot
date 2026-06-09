using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace VoiceRecogniseBot;

public static class SettingsEndpoints
{
    private static readonly Config ConfigStore = new();

    public static void MapSettingsEndpoints(WebApplication app)
    {
        app.MapGet("/settings", () =>
        {
            var json = Config.GetConfigContent();
            return Results.Content(json, "application/json", Encoding.UTF8);
        });

        app.MapPost("/settings", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();

            AppConfig? config;
            try
            {
                config = JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"Invalid settings JSON: {ex.Message}" });
            }

            var validationError = ValidateConfig(config);
            if (validationError is not null)
            {
                return Results.BadRequest(new { error = validationError });
            }

            ConfigStore.SaveAppConfig(config!);
            return Results.Json(config);
        });
    }

    private static string? ValidateConfig(AppConfig? config)
    {
        if (config is null)
        {
            return "Settings payload is empty or invalid.";
        }

        if (string.IsNullOrWhiteSpace(config.Model))
        {
            return "Model is required.";
        }

        if (string.IsNullOrWhiteSpace(config.Token))
        {
            return "Telegram token is required.";
        }

        config.Lang = config.Lang
            .Select(language => language.Trim().ToUpperInvariant())
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        config.DefaultLang = config.DefaultLang.Trim().ToUpperInvariant();

        if (config.Lang.Count == 0)
        {
            return "At least one recognition language is required.";
        }

        if (!config.Lang.Contains(config.DefaultLang, StringComparer.OrdinalIgnoreCase))
        {
            return "Default language must be included in recognition languages.";
        }

        return null;
    }
}
