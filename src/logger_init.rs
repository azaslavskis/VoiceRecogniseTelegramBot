//! A simple logger implementation using the `log` crate.

use log::{debug, error, info, warn};
use env_logger::Env;

/// A struct representing a basic logger.
pub struct Logger {}

impl Logger {

    pub fn init(){
        env_logger::Builder::from_env(Env::default().default_filter_or("debug")).init();
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
            4 => error!("Error:{}",message),
            _ => {
                error!("Logger caused an error. Programmer must have been really drunk");
            }
        }

        // Additional debug log statement outside the match block
        debug!("This is a debug message: {}", "message");
    }
}
