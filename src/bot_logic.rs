use crate::Config;
use crate::Logger;
use std::fs::File;
use std::io::BufReader;
use std::io::Read;
use std::path::Path;
use teloxide::prelude::*;
use teloxide::types::InputFile;
use teloxide::{prelude::*, utils::command::BotCommands};
#[derive(BotCommands, Clone)]
#[command(
    rename_rule = "lowercase",
    description = "These commands are supported:"
)]
enum Command {
    #[command(description = "sends logs as file")]
    log,
    #[command(description = "display this text.")]
    Help,
    #[command(description = "handle a username.")]
    Username(String),
    #[command(description = "handle a username and an age.", parse_with = "split")]
    UsernameAndAge { username: String, age: u8 },
}


async fn answer(bot: Bot, msg: Message, cmd: Command) -> ResponseResult<()> {
    match cmd {
        Command::Help => {
            bot.send_message(msg.chat.id, Command::descriptions().to_string())
                .await?
        }
        Command::Username(username) => {
            bot.send_message(msg.chat.id, format!("Your username is @{username}."))
                .await?
        }
        Command::UsernameAndAge { username, age } => {
            bot.send_message(
                msg.chat.id,
                format!("Your username is @{username} and age is {age}."),
            )
            .await?
        }
        Command::log => {
            let config = Config::init();
            let temp_path = config.data.path_config.temp_path.as_str();
            let log_file_path = format!("{}/log-voice-recognise_bot.log", temp_path);
            bot.send_document(msg.chat.id, InputFile::file(Path::new(&log_file_path)))
                .send()
                .await?
        }
    };

    Ok(())
}

pub async fn bot_worker() {
    let config = Config::init();
    let telegram_token = config.data.telegram_config.api_key;
    Logger::log(1, "Starting bot logic");
    let bot = Bot::new(telegram_token);
    Command::repl(bot, answer).await;
}
