//! A simple logger implementation using the `log` crate.

use env_logger::Env;
use log::{debug, error, info, warn};

use crate::Config;
use chrono::Local;
use log::*;
use std::fs::File;
use std::io::Write;

/// A struct representing a basic logger.
pub struct Logger {}

impl Logger {
    pub fn init() {
        let config = Config::init();
        let temp_path = config.data.path_config.temp_path.as_str();
        let log_file_path = format!("{}/log-voice-recognise_bot.log", temp_path);
        let target = Box::new(File::create(log_file_path).expect("Can't create file"));
        env_logger::Builder::new()
            .target(env_logger::Target::Pipe(target))
            .filter(None, LevelFilter::Debug)
            .format(|buf, record| {
                writeln!(
                    buf,
                    "[{} {} {}:{}] {}",
                    Local::now().format("%Y-%m-%d %H:%M:%S%.3f"),
                    record.level(),
                    record.file().unwrap_or("unknown"),
                    record.line().unwrap_or(0),
                    record.args()
                )
            })
            .init();

        //env_logger::Builder::from_env(Env::default().default_filter_or("debug")).init();
    }
    /// Logs a message at the specified level.
    ///
    /// # Arguments
    ///
    /// * `level` - The log level (1 for info, 2 for debug, 3 for warning).
    /// * `message` - The message to be logged.
    ///
    /// # Examples
    ///
    /// ```
    /// // Create a Logger instance
    /// let logger = Logger {};
    ///
    /// // Log an informational message
    /// logger.log(1, String::from("This is an information message"));
    ///
    /// // Log a debug message
    /// logger.log(2, String::from("This is a debug message"));
    ///
    /// // Log a warning message
    /// logger.log(3, String::from("This is a warning message"));
    /// ```
    pub fn log(level: usize, message: &str) {
        match level {
            1 => info!("Info: {}", message),
            2 => debug!("Debug: {}", message),
            3 => warn!("Warning: {}", message),
            4 => error!("Error: {}", message),
            _ => {
                error!("Logger caused an error. Programmer must have been really drunk");
            }
        }
    }
}
