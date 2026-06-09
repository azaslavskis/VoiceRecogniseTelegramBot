using CommandLine;
using Newtonsoft.Json;

namespace VoiceRecogniseBot;

public static class Program
{
    private static readonly Config Config = new();
    private static readonly TelegramBotLogger AppLog = new();
    private static readonly SettingsPathClass SettingsPath = new();

    [Verb("run", HelpText = "Start the Telegram bot and the local stats API. Use 'run bot' or 'run web-ui' to start only one service.")]
    private sealed class RunOptions
    {
        [Value(0, MetaName = "service", Required = false, HelpText = "Service to run: bot or web-ui. Omit to start the bot and use the configured web server setting.")]
        public string? Service { get; set; }

        [Option("web-server", HelpText = "Enable or disable the local stats web server for this run.")]
        public bool? WebServer { get; set; }
    }

    [Verb("config-show", HelpText = "Print the current JSON configuration.")]
    private sealed class ShowConfigOptions
    {
    }

    [Verb("config-path", HelpText = "Print the configuration file path.")]
    private sealed class ConfigPathOptions
    {
    }

    [Verb("stats-path", HelpText = "Print the stats file path.")]
    private sealed class StatsPathOptions
    {
    }

    [Verb("stats-show", HelpText = "Print saved message statistics.")]
    private sealed class ShowStatsOptions
    {
    }

    [Verb("config-set", HelpText = "Update one or more configuration values.")]
    private sealed class UpdateConfigOptions
    {
        [Option("model", HelpText = "Whisper model name or local model file path.")]
        public string? Model { get; set; }

        [Option("token", HelpText = "Telegram bot token.")]
        public string? Token { get; set; }

        [Option("lang", Separator = ',', HelpText = "Comma-separated language codes, for example EN,RU,LV.")]
        public IEnumerable<string>? Lang { get; set; }

        [Option("default-lang", HelpText = "Default recognition language.")]
        public string? DefaultLang { get; set; }

        [Option("web-server", HelpText = "Enable or disable the local stats web server.")]
        public bool? WebServer { get; set; }
    }

    public static async Task<int> Main(string[] args)
    {
        _ = new AppConfiguration();

        return await Parser.Default.ParseArguments<
                RunOptions,
                ShowConfigOptions,
                ConfigPathOptions,
                StatsPathOptions,
                ShowStatsOptions,
                UpdateConfigOptions>(args)
            .MapResult(
                (RunOptions options) => RunBotHost(options),
                (ShowConfigOptions _) => Task.FromResult(ShowConfig()),
                (ConfigPathOptions _) => Task.FromResult(PrintPath(SettingsPath.GetSettingPath())),
                (StatsPathOptions _) => Task.FromResult(PrintPath(SettingsPath.GetStatsPath())),
                (ShowStatsOptions _) => Task.FromResult(ShowStats()),
                (UpdateConfigOptions options) => Task.FromResult(UpdateConfiguration(options)),
                errors => Task.FromResult(errors.Any(error => error.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError or ErrorType.VersionRequestedError) ? 0 : 1));
    }

    private static Task<int> RunBotHost(RunOptions options)
    {
        return options.Service?.Trim().ToLowerInvariant() switch
        {
            null or "" => RunBotWithConfiguredWebServer(options),
            "bot" => RunBotOnly(),
            "web-ui" or "webui" or "web" => RunWebUiOnly(),
            _ => Task.FromResult(PrintUnknownService(options.Service))
        };
    }

    private static async Task<int> RunBotWithConfiguredWebServer(RunOptions options)
    {
        AppLog.logger.Info("Starting bot host");
        var appConfig = Config.LoadAppConfig();
        var shouldStartWebServer = options.WebServer ?? appConfig.WebServer;
        using var cancellationTokenSource = CreateShutdownTokenSource();

        var webUiTask = shouldStartWebServer
            ? new WebUi().ServerStartAsync(cancellationTokenSource.Token)
            : Task.CompletedTask;
        var telegramTask = new TelegramApi().RunAsync(cancellationTokenSource.Token);

        var completedTask = shouldStartWebServer
            ? await Task.WhenAny(telegramTask, webUiTask)
            : telegramTask;

        await completedTask;
        await cancellationTokenSource.CancelAsync();
        await AwaitShutdownAsync(completedTask == telegramTask ? webUiTask : telegramTask);
        return 0;
    }

    private static async Task<int> RunBotOnly()
    {
        AppLog.logger.Info("Starting Telegram bot only");
        using var cancellationTokenSource = CreateShutdownTokenSource();
        await new TelegramApi().RunAsync(cancellationTokenSource.Token);
        return 0;
    }

    private static async Task<int> RunWebUiOnly()
    {
        using var cancellationTokenSource = CreateShutdownTokenSource();
        await new WebUi().ServerStartAsync(cancellationTokenSource.Token);
        return 0;
    }

    private static CancellationTokenSource CreateShutdownTokenSource()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        return cancellationTokenSource;
    }

    private static async Task AwaitShutdownAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }



    private static int PrintUnknownService(string? service)
    {
        Console.Error.WriteLine($"Unknown run service '{service}'. Use 'run bot' or 'run web-ui'.");
        return 1;
    }

    private static int ShowConfig()
    {
        var config = Config.LoadAppConfig();
        Console.WriteLine(JsonConvert.SerializeObject(config, Formatting.Indented));
        return 0;
    }

    private static int ShowStats()
    {
        var stats = new StatsManager();
        Console.WriteLine(stats.GenerateJsonStats());
        return 0;
    }

    private static int UpdateConfiguration(UpdateConfigOptions options)
    {
        if (options.Model is null &&
            options.Token is null &&
            options.Lang is null &&
            options.DefaultLang is null &&
            options.WebServer is null)
        {
            Console.Error.WriteLine("No changes requested. Use at least one option such as --token or --lang.");
            return 1;
        }

        var updatedConfig = Config.LoadAppConfig();

        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            updatedConfig.Model = options.Model.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            updatedConfig.Token = options.Token.Trim();
        }

        if (options.Lang is not null)
        {
            var languages = options.Lang
                .Select(lang => lang.Trim())
                .Where(lang => !string.IsNullOrWhiteSpace(lang))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (languages.Count > 0)
            {
                updatedConfig.Lang = languages;
            }
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultLang))
        {
            updatedConfig.DefaultLang = options.DefaultLang.Trim();
        }
        

        if (options.WebServer is not null)
        {
            updatedConfig.WebServer = options.WebServer.Value;
        }

        if (updatedConfig.Lang.Count > 0 &&
            !updatedConfig.Lang.Contains(updatedConfig.DefaultLang, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Default language must also exist in the language list.");
            return 1;
        }

        Config.SaveAppConfig(updatedConfig);

        Console.WriteLine("Configuration updated:");
        Console.WriteLine(JsonConvert.SerializeObject(updatedConfig, Formatting.Indented));
        return 0;
    }

    private static int PrintPath(string path)
    {
        Console.WriteLine(path);
        return 0;
    }
}
