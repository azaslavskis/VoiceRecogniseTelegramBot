using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace VoiceRecogniseBot;

public class WebUi
{
    private static readonly SettingsPathClass SettingsPath = new();

    public void ServerStart()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://localhost:5010");

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

        app.Run();
    }
}
