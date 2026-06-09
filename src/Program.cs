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

    public static int Main(string[] args)
    {
        _ = new AppConfiguration();

        return Parser.Default.ParseArguments<
                RunOptions,
                ShowConfigOptions,
                ConfigPathOptions,
                StatsPathOptions,
                ShowStatsOptions,
                UpdateConfigOptions>(args)
            .MapResult(
                (RunOptions options) => RunBotHost(options),
                (ShowConfigOptions _) => ShowConfig(),
                (ConfigPathOptions _) => PrintPath(SettingsPath.GetSettingPath()),
                (StatsPathOptions _) => PrintPath(SettingsPath.GetStatsPath()),
                (ShowStatsOptions _) => ShowStats(),
                (UpdateConfigOptions options) => UpdateConfiguration(options),
                errors => errors.Any(error => error.Tag is ErrorType.HelpRequestedError or ErrorType.HelpVerbRequestedError or ErrorType.VersionRequestedError) ? 0 : 1);
    }

    private static int RunBotHost(RunOptions options)
    {
        return options.Service?.Trim().ToLowerInvariant() switch
        {
            null or "" => RunBotWithConfiguredWebServer(options),
            "bot" => RunBotOnly(),
            "web-ui" or "webui" or "web" => RunWebUiOnly(),
            _ => PrintUnknownService(options.Service)
        };
    }

    private static int RunBotWithConfiguredWebServer(RunOptions options)
    {
        AppLog.logger.Info("Starting bot host");
        var appConfig = Config.LoadAppConfig();
        var shouldStartWebServer = options.WebServer ?? appConfig.WebServer;

        //StartWebUiInBackground(shouldStartWebServer);

        _ = new TelegramApi();
        return 0;
    }

    private static int RunBotOnly()
    {
        AppLog.logger.Info("Starting Telegram bot only");
        _ = new TelegramApi();
        return 0;
    }

    private static int RunWebUiOnly()
    {
        new WebUi().ServerStart();
        return 0;
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
