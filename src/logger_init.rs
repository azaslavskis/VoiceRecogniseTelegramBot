use log::{debug, error, info, warn};

pub struct Logger {

}

impl Logger {
    pub fn log(level:usize,message:String) {
        match level {

            1 => info!(format!("Info:{}",error)),
            2 => debug!(format!("Debug:{}",error)),
            3 => warn!(format!("Warring:{}",error)),

            _ => { error!("logger caused an error. Programmer been really drunk")},

        }
        debug!("this is a debug {}", "message");

    }

}