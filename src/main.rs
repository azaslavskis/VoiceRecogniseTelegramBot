mod logger_init;
use logger_init::Logger;
use crate::config_parsing::Config;

mod utils;
mod config_parsing;
mod language_model_declaration;

fn main() {
    Logger::init();
    Logger::log(1,"App loaded ok");
    Config::init();


}
