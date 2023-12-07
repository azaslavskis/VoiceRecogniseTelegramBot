use crate::language_model_declaration::LanguageModel;
use serde_derive::Deserialize;
// Import the required dependencies.
use crate::logger_init::Logger;

use std::fs;
use std::process::exit;
use toml;

#[derive(Debug, Clone)]
pub struct Config {
    pub data: Data,
}
#[derive(Deserialize, Debug, Clone)]
pub struct Data {
    pub path_config: Path_Config,
    pub model_options: Model_Options,
    pub telegram_config: Telegram_Config,
}
#[derive(Deserialize, Debug, Clone)]
pub struct Path_Config {
    pub config_path: String,
    pub model_path: String,
    pub temp_path: String,
}
#[derive(Deserialize, Debug, Clone)]
pub struct Model_Options {
    pub model: String,
    pub language_target: String,
}
#[derive(Deserialize, Debug, Clone)]
pub struct Telegram_Config {
    pub api_key: String,
    pub chat_id: String,
}
impl Config {
    pub fn init() -> Self {
        let filename = "/etc/recognise_bot/config.toml";

        // Read the contents of the file using a `match` block
        // to return the `data: Ok(c)` as a `String`
        // or handle any `errors: Err(_)`.
        let contents = match fs::read_to_string(filename) {
            // If successful return the files text as `contents`.
            // `c` is a local variable.
            Ok(c) => c,
            // Handle the `error` case.
            Err(_) => {
                // Write `msg` to `stderr`.
                eprintln!("Could not read file `{}`", filename);
                // Exit the program with exit code `1`.
                exit(1);
            }
        };
        Logger::log(1, contents.as_str());

        let data: Data = match toml::from_str(&contents) {
            // If successful, return data as `Data` struct.
            // `d` is a local variable.
            Ok(d) => d,
            // Handle the `error` case.
            Err(_) => {
                // Write `msg` to `stderr`.
                Logger::log(
                    4,
                    format!("Unable to load data from `{}`", filename).as_str(),
                );
                // Exit the program with exit code `1`.
                exit(1);
            }
        };

        return Config { data };
    }
}
