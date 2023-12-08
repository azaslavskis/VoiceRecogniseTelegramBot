mod logger_init;
use crate::config_parsing::Config;
use crate::get_model_to_install::get_model;
use crate::language_model_declaration::LanguageModel;
use logger_init::Logger;
use std::path::Path;
use teloxide::prelude::*;
mod config_parsing;
mod get_model_to_install;
use std::fs::File;
use std::io::BufReader;
use std::io::Read;
use tokio::runtime::Runtime;
mod bot_logic;
mod language_model_declaration;
use crate::bot_logic::bot_worker;

mod utils;

fn main() {
    Logger::init();
    Logger::log(1, "App loaded ok");
    Logger::log(1, "Checking is model installed?");
    let config = Config::init();
    if Path::new(config.data.path_config.model_path.as_str()).exists() {
        Logger::log(1, "Model been found... skiping downloading");
    } else {
        let model = LanguageModel::from_str(&config.data.model_options.model);
        match model {
            Some(model) => get_model::get_model(model),
            None => Logger::log(4, "Imposible recognise model"),
        }
    }

    let rt = Runtime::new().unwrap();
    rt.block_on(bot_worker());
}
