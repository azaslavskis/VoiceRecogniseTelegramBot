mod logger_init;
use crate::config_parsing::Config;
use logger_init::Logger;
use crate::get_model_to_install::get_model;
use crate::language_model_declaration::LanguageModel;

mod config_parsing;
mod language_model_declaration;
mod utils;
mod get_model_to_install;

fn main() {
    Logger::init();
    Logger::log(1, "App loaded ok");
    let mut config = Config::init();
    get_model::get_model(LanguageModel::Base);
    //println!("{}",config.data.telegram_config.api_key);

}
