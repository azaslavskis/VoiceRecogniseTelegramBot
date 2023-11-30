mod logger_init;
use logger_init::Logger;
mod utils;

fn main() {
    Logger::init();
    Logger::log(1,"App loaded ok");


}
