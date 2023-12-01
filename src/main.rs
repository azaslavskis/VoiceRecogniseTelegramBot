mod logger_init;
use crate::config_parsing::Config;
use logger_init::Logger;

mod config_parsing;
mod language_model_declaration;
mod utils;

fn main() {
    Logger::init();
    Logger::log(1, "App loaded ok");
    let mut config = Config::init();
    //println!("{}",config.data.telegram_config.api_key);
    
}
