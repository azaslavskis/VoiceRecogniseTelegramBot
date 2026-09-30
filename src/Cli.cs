using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace VoiceRecogniseBot;

/// <summary>
/// Defines the command-line interface. See docs/cli.md for the user-facing reference.
/// </summary>
internal static class Cli
{
    private static readonly Option<string?> TokenOption = new("--token")
    {
        Description = "Telegram bot token from @BotFather."
    };

    private static readonly Option<string?> ModelOption = new("--model")
    {
        Description = "Whisper model name (see 'model list') or path to a local model file."
    };

    private static readonly Option<string[]?> LangOption = new("--lang")
    {
        Description = "Recognition languages offered to users, comma-separated, for example EN,RU,LV.",
        AllowMultipleArgumentsPerToken = true,
        CustomParser = result => result.Tokens
            .SelectMany(token => token.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray()
    };

    private static readonly Option<string?> DefaultLangOption = new("--default-lang")
    {
        Description = "Language used until a chat picks another one. Must be one of --lang."
    };

    private static readonly Option<bool?> WebUiOption = new("--web-ui")
    {
        Description = "Whether a plain 'run' also starts the web UI.",
        HelpName = "true|false"
    };

    // Name used before 2.2; still accepted so existing scripts keep working.
    private static readonly Option<bool?> LegacyWebServerOption = new("--web-server")
    {
        Hidden = true
    };

    private static readonly Option<string[]?> TextOption = new("--text")
    {
        Description = "Bot message or button label as Key=Value, for example --text \"AboutButton=Info\". Repeatable; see 'config show' for the keys."
    };

    private static readonly Option<bool> JsonOption = new("--json")
    {
        Description = "Print machine-readable JSON."
    };

    public static RootCommand Build()
    {
        var root = new RootCommand("Telegram bot that transcribes voice, audio and video messages locally with Whisper.")
        {
            BuildRunCommand(),
            BuildConfigCommand(),
            BuildStatsCommand(),
            BuildLogsCommand(),
            BuildModelCommand(),
            BuildTranscribeCommand(),
            BuildInfoCommand(),

            // Flat command names used before 2.2.
            BuildConfigShowCommand("config-show", hidden: true),
            BuildConfigSetCommand("config-set", hidden: true),
            BuildPathCommand("config-path", "Print the configuration file path.", () => AppPaths.SettingsFile, hidden: true),
            BuildStatsShowCommand("stats-show", hidden: true),
            BuildPathCommand("stats-path", "Print the stats file path.", () => AppPaths.StatsFile, hidden: true)
        };

        return root;
    }

    // ------------------------------------------------------------ run

    private static Command BuildRunCommand()
    {
        var serviceArgument = new Argument<string>("service")
        {
            Description = "What to start: 'bot', 'web-ui' or 'all'. When omitted, starts the bot and, if enabled in the configuration, the web UI.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => string.Empty
        };

        var urlsOption = new Option<string?>("--urls")
        {
            Description = $"Address the web UI listens on, for example http://0.0.0.0:5010. Overrides {WebUi.UrlsVariable}."
        };

        var command = new Command("run", "Start the Telegram bot and the web UI.")
        {
            serviceArgument,
            urlsOption,
            LegacyWebServerOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            bool runBot, runWebUi;
            var service = parseResult.GetValue(serviceArgument)!.Trim().ToLowerInvariant();
            switch (service)
            {
                case "":
                    runBot = true;
                    runWebUi = parseResult.GetValue(LegacyWebServerOption) ?? ConfigStore.Load().WebServer;
                    break;
                case "all":
                    runBot = runWebUi = true;
                    break;
                case "bot":
                    runBot = true;
                    runWebUi = false;
                    break;
                case "web-ui" or "webui" or "web":
                    runBot = false;
                    runWebUi = true;
                    break;
                default:
                    return Fail($"Unknown service '{service}'. Use 'bot', 'web-ui' or 'all'.");
            }

            AppLog.Logger.Info("Starting VoiceRecogniseBot {0} (bot: {1}, web UI: {2})", AppInfo.Version, runBot, runWebUi);

            using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var telegramApi = runBot ? new TelegramApi() : null;

            var tasks = new List<Task>();
            if (telegramApi is not null)
            {
                tasks.Add(telegramApi.RunAsync(shutdown.Token));
            }

            if (runWebUi)
            {
                tasks.Add(WebUi.RunAsync(parseResult.GetValue(urlsOption), shutdown.Token));
            }

            // When either service stops, for any reason, take the other one down with it.
            var firstCompleted = await Task.WhenAny(tasks);
            await shutdown.CancelAsync();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (!firstCompleted.IsFaulted)
            {
            }

            return 0;
        });

        return command;
    }

    // ------------------------------------------------------------ config

    private static Command BuildConfigCommand()
    {
        return new Command("config", "Show or change the configuration.")
        {
            BuildConfigShowCommand("show"),
            BuildConfigSetCommand("set"),
            BuildPathCommand("path", "Print the configuration file path.", () => AppPaths.SettingsFile)
        };
    }

    private static Command BuildConfigShowCommand(string name, bool hidden = false)
    {
        var showTokenOption = new Option<bool>("--show-token")
        {
            Description = "Print the bot token instead of masking it."
        };

        var command = new Command(name, "Print the current configuration as JSON.") { showTokenOption };
        command.Hidden = hidden;
        command.SetAction(parseResult =>
        {
            PrintConfig(ConfigStore.Load(), parseResult.GetValue(showTokenOption));
            return 0;
        });

        return command;
    }

    private static Command BuildConfigSetCommand(string name, bool hidden = false)
    {
        var command = new Command(name, "Change one or more configuration values.")
        {
            TokenOption,
            ModelOption,
            LangOption,
            DefaultLangOption,
            WebUiOption,
            TextOption,
            LegacyWebServerOption
        };
        command.Hidden = hidden;

        command.SetAction(parseResult =>
        {
            var token = parseResult.GetValue(TokenOption);
            var model = parseResult.GetValue(ModelOption);
            // An option that was not passed parses to an empty list rather than null.
            var languages = parseResult.GetValue(LangOption) is { Length: > 0 } passedLanguages ? passedLanguages : null;
            var defaultLanguage = parseResult.GetValue(DefaultLangOption);
            var webUi = parseResult.GetValue(WebUiOption) ?? parseResult.GetValue(LegacyWebServerOption);
            var texts = parseResult.GetValue(TextOption) ?? [];

            if (token is null && model is null && languages is null && defaultLanguage is null && webUi is null && texts.Length == 0)
            {
                return Fail("Nothing to change. Pass at least one option, for example --token; see 'config set --help'.");
            }

            var config = ConfigStore.Load();
            config.Token = token ?? config.Token;
            config.Model = model ?? config.Model;
            config.Lang = languages?.ToList() ?? config.Lang;
            config.DefaultLang = defaultLanguage ?? config.DefaultLang;
            config.WebServer = webUi ?? config.WebServer;

            foreach (var text in texts)
            {
                if (SetBotText(config.BotText, text) is { } textError)
                {
                    return Fail(textError);
                }
            }

            ConfigStore.Normalize(config);
            if (ConfigStore.Validate(config) is { } validationError)
            {
                return Fail(validationError);
            }

            ConfigStore.Save(config);

            Console.WriteLine("Configuration updated:");
            PrintConfig(config, showToken: false);
            return 0;
        });

        return command;
    }

    /// <summary>
    /// Applies one "Key=Value" bot text assignment. Returns an error message for a malformed or unknown key.
    /// </summary>
    private static string? SetBotText(BotTextConfig botText, string assignment)
    {
        var separator = assignment.IndexOf('=');
        var key = separator > 0 ? assignment[..separator].Trim() : string.Empty;
        var property = typeof(BotTextConfig).GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null)
        {
            var knownKeys = string.Join(", ", typeof(BotTextConfig).GetProperties().Select(p => p.Name));
            return separator > 0
                ? $"Unknown bot text '{key}'. Known keys: {knownKeys}."
                : $"'{assignment}' is not in Key=Value form. Known keys: {knownKeys}.";
        }

        property.SetValue(botText, assignment[(separator + 1)..]);
        return null;
    }

    private static void PrintConfig(AppConfig config, bool showToken)
    {
        var printable = JsonSerializer.SerializeToNode(config, ConfigStore.JsonOptions)!;
        if (!showToken && ConfigStore.IsTokenConfigured(config.Token))
        {
            // Keep the bot id (the part before the colon) so different bots stay distinguishable.
            var separator = config.Token.IndexOf(':');
            printable[nameof(AppConfig.Token)] = (separator > 0 ? config.Token[..(separator + 1)] : string.Empty) + "********";
        }

        Console.WriteLine(printable.ToJsonString(ConfigStore.JsonOptions));
    }

    // ------------------------------------------------------------ stats

    private static Command BuildStatsCommand()
    {
        var yesOption = new Option<bool>("--yes", "-y")
        {
            Description = "Do not ask for confirmation."
        };

        var resetCommand = new Command("reset", "Delete all message statistics.") { yesOption };
        resetCommand.SetAction(parseResult =>
        {
            if (!parseResult.GetValue(yesOption))
            {
                Console.Write($"Delete all statistics in {AppPaths.StatsFile}? [y/N] ");
                if (Console.ReadLine()?.Trim().ToLowerInvariant() is not ("y" or "yes"))
                {
                    Console.WriteLine("Nothing deleted.");
                    return 1;
                }
            }

            StatsStore.Reset();
            Console.WriteLine("Statistics reset.");
            return 0;
        });

        return new Command("stats", "Show or reset message statistics.")
        {
            BuildStatsShowCommand("show"),
            resetCommand,
            BuildPathCommand("path", "Print the stats file path.", () => AppPaths.StatsFile)
        };
    }

    private static Command BuildStatsShowCommand(string name, bool hidden = false)
    {
        var command = new Command(name, "Print message statistics.") { JsonOption };
        command.Hidden = hidden;
        command.SetAction(parseResult =>
        {
            var summary = StatsStore.GetSummary();
            if (parseResult.GetValue(JsonOption))
            {
                Console.WriteLine(JsonSerializer.Serialize(summary, ConfigStore.JsonOptions));
                return 0;
            }

            Console.WriteLine($"Messages, all time:        {summary.TotalMessages}");
            Console.WriteLine($"Transcriptions, all time:  {summary.TotalTranscriptions}");
            Console.WriteLine($"Messages, last 7 days:     {summary.MessagesPast7Days}");
            Console.WriteLine();
            Console.WriteLine("Date (UTC)   Messages  Transcriptions");
            foreach (var day in summary.Daily)
            {
                Console.WriteLine($"{day.Date}   {day.Messages,8}  {day.Transcriptions,14}");
            }

            return 0;
        });

        return command;
    }

    // ------------------------------------------------------------ logs

    private static Command BuildLogsCommand()
    {
        var linesOption = new Option<int>("--lines", "-n")
        {
            Description = "Number of most recent lines to print.",
            DefaultValueFactory = _ => 50
        };

        var followOption = new Option<bool>("--follow", "-f")
        {
            Description = "Keep printing new log lines until interrupted."
        };

        var command = new Command("logs", "Print the most recent log lines.")
        {
            linesOption,
            followOption,
            BuildPathCommand("path", "Print the log file path.", () => AppPaths.LogFile)
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            foreach (var line in AppLog.ReadTail(Math.Max(parseResult.GetValue(linesOption), 0)))
            {
                Console.WriteLine(line);
            }

            if (parseResult.GetValue(followOption))
            {
                await FollowLogAsync(cancellationToken);
            }

            return 0;
        });

        return command;
    }

    private static async Task FollowLogAsync(CancellationToken cancellationToken)
    {
        var position = File.Exists(AppPaths.LogFile) ? new FileInfo(AppPaths.LogFile).Length : 0;

        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                if (!File.Exists(AppPaths.LogFile))
                {
                    continue;
                }

                using var stream = new FileStream(AppPaths.LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                // The file shrinks when it is rotated; start again from the top of the new one.
                if (stream.Length < position)
                {
                    position = 0;
                }

                stream.Seek(position, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                Console.Write(await reader.ReadToEndAsync(cancellationToken));
                position = stream.Length;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // ------------------------------------------------------------ model

    private static Command BuildModelCommand()
    {
        var listCommand = new Command("list", "List the Whisper models that can be downloaded and the ones already on disk.");
        listCommand.SetAction(_ =>
        {
            var activeModel = ConfigStore.Load().Model;
            var models = WhisperApi.DownloadableModels
                .Concat(WhisperApi.GetLocalModels())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var model in models)
            {
                var file = new FileInfo(AppPaths.GetManagedModelPath(model));
                var status = file.Exists ? $"downloaded, {file.Length / (1024 * 1024)} MB" : "not downloaded";
                var marker = string.Equals(model, activeModel, StringComparison.OrdinalIgnoreCase) ? "*" : " ";
                Console.WriteLine($"{marker} {model,-22} {status}");
            }

            Console.WriteLine();
            Console.WriteLine($"* = model in use. Models are stored in {AppPaths.ModelsDirectory}");
            return 0;
        });

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Model to download. Defaults to the configured model.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var downloadCommand = new Command("download", "Download a Whisper model now instead of on the first transcription.") { nameArgument };
        downloadCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var model = parseResult.GetValue(nameArgument)?.Trim();
            if (string.IsNullOrEmpty(model))
            {
                model = ConfigStore.Load().Model;
            }

            if (WhisperApi.DescribeModelProblem(model) is { } problem)
            {
                return Fail(problem);
            }

            var path = WhisperApi.GetModelPath(model);
            if (File.Exists(path))
            {
                Console.WriteLine($"{model} is already available at {path}");
                return 0;
            }

            Console.WriteLine($"Downloading {model}...");
            await WhisperApi.DownloadModelAsync(model, cancellationToken);
            Console.WriteLine($"Saved to {path}");
            return 0;
        });

        return new Command("model", "List or download Whisper models.")
        {
            listCommand,
            downloadCommand
        };
    }

    // ------------------------------------------------------------ transcribe

    private static Command BuildTranscribeCommand()
    {
        var fileArgument = new Argument<FileInfo>("file")
        {
            Description = "Audio or video file in any format ffmpeg can read."
        };

        var languageOption = new Option<string?>("--lang", "-l")
        {
            Description = "Language spoken in the file. Defaults to the configured default language."
        };

        var command = new Command("transcribe", "Transcribe a local media file, without Telegram. Useful for checking the model and ffmpeg setup.")
        {
            fileArgument,
            languageOption,
            ModelOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArgument)!;
            if (!file.Exists)
            {
                return Fail($"File '{file.FullName}' does not exist.");
            }

            var config = ConfigStore.Load();
            var model = parseResult.GetValue(ModelOption)?.Trim() ?? config.Model;
            if (WhisperApi.DescribeModelProblem(model) is { } problem)
            {
                return Fail(problem);
            }

            using var whisper = new WhisperApi();
            Console.Write(await whisper.TranscribeAsync(file.FullName, model, parseResult.GetValue(languageOption) ?? config.DefaultLang, cancellationToken));
            return 0;
        });

        return command;
    }

    // ------------------------------------------------------------ info

    private static Command BuildInfoCommand()
    {
        var command = new Command("info", "Print the version, file locations and a quick setup check.");
        command.SetAction(_ =>
        {
            var config = ConfigStore.Load();
            var modelPath = WhisperApi.GetModelPath(config.Model);

            Console.WriteLine($"Version:         {AppInfo.Version}");
            Console.WriteLine($"Data directory:  {AppPaths.DataDirectory}");
            Console.WriteLine($"Configuration:   {AppPaths.SettingsFile}");
            Console.WriteLine($"Statistics:      {AppPaths.StatsFile}");
            Console.WriteLine($"Log file:        {AppPaths.LogFile}");
            Console.WriteLine($"Web UI address:  {WebUi.ResolveUrls(null)}{(config.WebServer ? string.Empty : " (disabled in the configuration)")}");
            Console.WriteLine();
            Console.WriteLine($"Bot token:       {(ConfigStore.IsTokenConfigured(config.Token) ? "configured" : "NOT SET - run 'config set --token <token>'")}");
            Console.WriteLine($"Whisper model:   {config.Model} ({(File.Exists(modelPath) ? "downloaded" : "will be downloaded on first use")})");
            Console.WriteLine($"Languages:       {string.Join(", ", config.Lang)} (default {config.DefaultLang})");
            Console.WriteLine($"ffmpeg:          {AudioToWav.FindFfmpeg() ?? "NOT FOUND - install ffmpeg or set FFMPEG_PATH"}");
            return 0;
        });

        return command;
    }

    // ------------------------------------------------------------ helpers

    private static Command BuildPathCommand(string name, string description, Func<string> getPath, bool hidden = false)
    {
        var command = new Command(name, description) { Hidden = hidden };
        command.SetAction(_ =>
        {
            Console.WriteLine(getPath());
            return 0;
        });

        return command;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return 1;
    }
}

internal static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    public static DateTime StartedAtUtc { get; } = Process.GetCurrentProcess().StartTime.ToUniversalTime();
}
