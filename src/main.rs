mod logger_init;
use crate::config_parsing::Config;
use crate::get_model_to_install::get_model;
use crate::language_model_declaration::LanguageModel;
use logger_init::Logger;

mod config_parsing;
mod get_model_to_install;
mod language_model_declaration;
mod utils;

fn main() {
    Logger::init();
    Logger::log(1, "App loaded ok");
    let mut config = Config::init();
    get_model::get_model(LanguageModel::Small);
    //println!("{}",config.data.telegram_config.api_key);
}
