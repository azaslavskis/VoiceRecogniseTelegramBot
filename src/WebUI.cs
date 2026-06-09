using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace VoiceRecogniseBot;

public class WebUi
{
    private static readonly SettingsPathClass SettingsPath = new();

    public void ServerStart()
    {
        ServerStartAsync().GetAwaiter().GetResult();
    }

    public async Task ServerStartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        var urls = Environment.GetEnvironmentVariable("VOICE_RECOGNISEBOT_WEB_URLS");
        builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(urls) ? "http://localhost:5010" : urls);

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Json(new
        {
            status = "online",
            startedAtUtc = DateTime.UtcNow,
            settingsPath = SettingsPath.GetSettingPath(),
            statsPath = SettingsPath.GetStatsPath()
        }));

        SettingsEndpoints.MapSettingsEndpoints(app);

        app.MapGet("/stats", () =>
        {
            var json = new StatsManager().GenerateJsonStats();
            return Results.Content(json, "application/json");
        });

        await app.RunAsync(cancellationToken);
    }
}
