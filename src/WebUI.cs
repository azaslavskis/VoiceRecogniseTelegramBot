using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VoiceRecogniseBot;

/// <summary>
/// Hosts the admin web UI and the JSON endpoints it uses.
/// </summary>
internal static class WebUi
{
    public const string UrlsVariable = "VOICE_RECOGNISEBOT_WEB_URLS";

    private const string DefaultUrl = "http://localhost:5010";
    private const int MaxLogLines = 1000;

    /// <summary>
    /// Picks the listen address: the explicit value, then the environment variable, then localhost.
    /// </summary>
    public static string ResolveUrls(string? urls)
    {
        if (!string.IsNullOrWhiteSpace(urls))
        {
            return urls;
        }

        var environmentUrls = Environment.GetEnvironmentVariable(UrlsVariable);
        return string.IsNullOrWhiteSpace(environmentUrls) ? DefaultUrl : environmentUrls;
    }

    public static async Task RunAsync(string? urls, CancellationToken cancellationToken)
    {
        // The static files live next to the executable, regardless of the working directory.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.WebHost.UseUrls(ResolveUrls(urls));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        // Responses use the same property names as appsettings.json.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = null;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Json(new
        {
            Status = "online",
            AppInfo.Version,
            AppInfo.StartedAtUtc,
            Bot = BotStatus.Current,
            SettingsPath = AppPaths.SettingsFile,
            StatsPath = AppPaths.StatsFile,
            LogPath = AppPaths.LogFile
        }));

        app.MapGet("/stats", () => Results.Json(StatsStore.GetSummary()));

        app.MapGet("/logs", (int? lines) => Results.Json(new
        {
            Path = AppPaths.LogFile,
            Lines = AppLog.ReadTail(Math.Clamp(lines ?? 200, 1, MaxLogLines))
        }));

        app.MapGet("/settings", () => SettingsResponse(ConfigStore.Load()));
        app.MapPost("/settings", SaveSettingsAsync);

        await app.StartAsync(cancellationToken);
        AppLog.Logger.Info("Web UI listening on {0}", string.Join(", ", app.Urls));
        await app.WaitForShutdownAsync(cancellationToken);
    }

    private static async Task<IResult> SaveSettingsAsync(HttpRequest request)
    {
        // Browsers cannot send a JSON content type cross-origin without a CORS preflight, which
        // this server never approves, so this keeps other websites from changing the settings.
        if (!request.HasJsonContentType())
        {
            return Results.Json(new { Error = "Content-Type must be application/json." }, statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        AppConfig? config;
        try
        {
            config = await JsonSerializer.DeserializeAsync<AppConfig>(request.Body, ConfigStore.JsonOptions, request.HttpContext.RequestAborted);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { Error = $"Invalid settings JSON: {ex.Message}" });
        }

        if (config is null)
        {
            return Results.BadRequest(new { Error = "Settings payload is empty." });
        }

        ConfigStore.Normalize(config);

        // The token is never sent to the browser, so an empty value means "keep the current one".
        if (config.Token.Length == 0)
        {
            config.Token = ConfigStore.Load().Token;
        }

        if (ConfigStore.Validate(config) is { } validationError)
        {
            return Results.BadRequest(new { Error = validationError });
        }

        ConfigStore.Save(config);
        return SettingsResponse(config);
    }

    private static IResult SettingsResponse(AppConfig config)
    {
        return Results.Json(new
        {
            Settings = new AppConfig
            {
                Model = config.Model,
                Token = string.Empty,
                WebServer = config.WebServer,
                Lang = config.Lang,
                DefaultLang = config.DefaultLang,
                BotText = config.BotText
            },
            TokenConfigured = ConfigStore.IsTokenConfigured(config.Token)
        });
    }
}
