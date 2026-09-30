# Command-line reference

Everything the bot can do is available from one executable. How you call it depends on how it
was installed:

| Installation | Command |
| --- | --- |
| `.deb` / `.rpm` package | `voice-recognise-bot` |
| AppImage | `./VoiceRecogniseBot-<version>-x86_64.AppImage` |
| tarball or zip | `./VoiceRecogniseBot` (`VoiceRecogniseBot.exe` on Windows) |
| from source | `dotnet run --project src/VoiceRecogniseBot.csproj --` |
| Docker | `docker run … voice-recognise-bot` |

The examples below use `voice-recognise-bot`. Add `--help` to any command to see its options,
and `--version` to print the version.

```text
voice-recognise-bot
├── run [bot|web-ui|all]      start the bot and/or the web UI
├── config
│   ├── show                  print the configuration
│   ├── set                   change configuration values
│   └── path                  print the configuration file path
├── stats
│   ├── show                  print message statistics
│   ├── reset                 delete all statistics
│   └── path                  print the stats file path
├── logs [-n N] [-f]          print recent log lines
│   └── path                  print the log file path
├── model
│   ├── list                  list Whisper models
│   └── download [name]       download a model ahead of time
├── transcribe <file>         transcribe a local file, without Telegram
└── info                      version, file locations and a setup check
```

Commands exit with `0` on success and `1` on an error; error messages go to standard error.

## Which configuration a command uses

The configuration, statistics, logs and downloaded models live in one data directory:

| Situation | Data directory |
| --- | --- |
| `VOICE_RECOGNISEBOT_HOME` is set | that directory |
| Linux | `${XDG_CONFIG_HOME:-~/.config}/VoiceRecogniseBot` |
| Windows | `%LocalAppData%\VoiceRecogniseBot` |
| macOS | `~/Library/Application Support/VoiceRecogniseBot` |
| service installed from the `.deb` / `.rpm` | `/var/lib/voice-recognise-bot` |

With the `.deb` / `.rpm` package, **run commands with `sudo` to manage the system service**:
`sudo voice-recognise-bot …` runs as the service user against `/var/lib/voice-recognise-bot`.
Without `sudo` the command works on your own per-user configuration instead, which the service
does not read.

`voice-recognise-bot info` always shows which files are in use.

## First-time setup

```bash
# 1. Give the bot its token (create the bot with @BotFather in Telegram first)
voice-recognise-bot config set --token "123456:ABC-DEF"

# 2. Optional: pick the languages and a model, and download the model now
voice-recognise-bot config set --lang EN,RU,LV --default-lang EN --model ggml-small
voice-recognise-bot model download

# 3. Check the setup, then start
voice-recognise-bot info
voice-recognise-bot run
```

With the `.deb` / `.rpm` package the service is already running after installation, so step 1
(with `sudo`) is all that is needed; the bot connects within a few seconds of the token being
saved. The same settings can be changed in the web UI at <http://localhost:5010>.

## run

```text
voice-recognise-bot run [bot|web-ui|all] [--urls <urls>]
```

Starts the services and keeps running until interrupted with Ctrl+C or `SIGTERM`.

| Service | What starts |
| --- | --- |
| *(omitted)* | the bot, plus the web UI if `WebServer` is enabled in the configuration (the default) |
| `bot` | only the bot |
| `web-ui` | only the web UI, for example next to a bot that runs as a separate process |
| `all` | the bot and the web UI, regardless of the configuration |

| Option | Description |
| --- | --- |
| `--urls <urls>` | Address the web UI listens on, for example `http://0.0.0.0:5010`. Overrides `VOICE_RECOGNISEBOT_WEB_URLS`; the default is `http://localhost:5010`. |

Settings are re-read while the bot runs. Changing the token, model, languages or bot messages,
from the command line or the web UI, takes effect without a restart. If no token is configured
yet, the bot waits for one instead of exiting.

The web UI has no login. It listens on `localhost` by default; only bind it to other addresses
on a network you trust.

## config

### config show

```text
voice-recognise-bot config show [--show-token]
```

Prints the configuration as JSON. The bot token is masked unless `--show-token` is passed.

### config set

```text
voice-recognise-bot config set [options]
```

Changes the given values and leaves everything else as it is.

| Option | Description |
| --- | --- |
| `--token <token>` | Telegram bot token from @BotFather. |
| `--model <model>` | Whisper model name (see [`model list`](#model-list)) or the path to a local model file. |
| `--lang <codes>` | Recognition languages offered to users, comma-separated: `--lang EN,RU,LV`. |
| `--default-lang <code>` | Language used until a chat picks another one. Must be one of `--lang`. |
| `--web-ui <true\|false>` | Whether a plain `run` also starts the web UI. |
| `--text <Key=Value>` | A bot message or button label. Repeatable. |

The keys for `--text` are the ones under `BotText` in `config show`: `SetLanguageButton`,
`LogButton`, `AboutButton`, `MainMenuPrompt`, `LanguagePrompt`, `AboutMessage`,
`UnknownCommandMessage`, `TranscriptionInProgressMessage`, `TranscriptionResultPrefix`,
`LanguageChangedPrefix` and `InternalErrorMessage`.

```bash
voice-recognise-bot config set --lang EN,DE --default-lang DE
voice-recognise-bot config set --text "AboutButton=Info" --text "AboutMessage=Send me a voice message."
```

The command refuses to save a configuration that would not work: an unknown model name, a
missing model file, an empty language list, or a default language that is not in the list.

### config path

Prints the path of `appsettings.json`. The file can also be edited by hand; the running bot
picks up the change.

## stats

| Command | Description |
| --- | --- |
| `stats show` | Totals and a per-day table for the last 14 days. |
| `stats show --json` | The same data as JSON, for scripts. |
| `stats reset` | Deletes all statistics after a confirmation. `--yes` / `-y` skips the question. |
| `stats path` | Prints the path of `stats.json`. |

## logs

```text
voice-recognise-bot logs [--lines <n>] [--follow]
```

| Option | Description |
| --- | --- |
| `-n`, `--lines <n>` | Number of most recent lines to print. Default: 50. |
| `-f`, `--follow` | Keep printing new lines until interrupted. |

`logs path` prints the log file location. The log is rotated at 2 MB and three old files are kept.

When the bot runs as the packaged systemd service, `journalctl -u voice-recognise-bot -f` shows
the same messages.

## model

### model list

Lists the models that can be downloaded, marks the ones already on disk with their size, and
marks the model in use with `*`.

| Model | Notes |
| --- | --- |
| `ggml-tiny`, `ggml-base` | Fastest; fine for clear speech. `ggml-base` is the default. |
| `ggml-small`, `ggml-medium` | Slower, noticeably more accurate. |
| `ggml-large-v1`, `ggml-large-v2`, `ggml-large-v3`, `ggml-large-v3-turbo` | Most accurate; need several GB of memory. |
| `ggml-tiny.en`, `ggml-base.en`, `ggml-small.en`, `ggml-medium.en` | English-only variants. |

### model download

```text
voice-recognise-bot model download [name]
```

Downloads a model into the data directory. Without a name it downloads the configured model.
This is optional: a missing model is downloaded automatically on the first transcription, which
makes that first reply slow.

## transcribe

```text
voice-recognise-bot transcribe <file> [--lang <code>] [--model <model>]
```

Transcribes a local audio or video file and prints the result, exactly as the bot would reply
in Telegram. Useful for checking that ffmpeg and the model work before involving Telegram, and
for comparing models.

| Option | Description |
| --- | --- |
| `-l`, `--lang <code>` | Language spoken in the file. Defaults to the configured default language. |
| `--model <model>` | Model to use for this run. Defaults to the configured model. |

```bash
voice-recognise-bot transcribe voice-message.ogg --lang EN
```

## info

Prints the version, the files in use, the web UI address, and whether the token, the model and
ffmpeg are in place. Start here when something does not work.

## Environment variables

| Variable | Description |
| --- | --- |
| `VOICE_RECOGNISEBOT_HOME` | Data directory for configuration, statistics, logs and models. |
| `VOICE_RECOGNISEBOT_WEB_URLS` | Address the web UI listens on. Default `http://localhost:5010`. |
| `FFMPEG_PATH` | Folder containing the `ffmpeg` executable, if it is not on `PATH`. |

For the packaged systemd service, set these in `/etc/default/voice-recognise-bot` and restart
the service.

## Commands from versions before 2.2

The old flat command names still work, so existing scripts do not need to change:

| Old | New |
| --- | --- |
| `config-show` | `config show` |
| `config-set` | `config set` |
| `config-path` | `config path` |
| `stats-show` | `stats show` |
| `stats-path` | `stats path` |
| `--web-server <true\|false>` | `--web-ui <true\|false>` on `config set`; `run all` / `run bot` on `run` |

Two things behave differently from 2.1: `config show` masks the token unless `--show-token` is
passed, and `stats show` prints a table unless `--json` is passed.
