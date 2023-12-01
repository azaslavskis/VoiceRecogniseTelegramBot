use std::error::Error;
use std::fmt::format;
use std::fs::{copy, File};

use crate::config_parsing::Config;
use crate::language_model_declaration::LanguageModel;
use crate::logger_init::Logger;
use error_chain::error_chain;


use tempfile::Builder;

pub struct get_model {

}


impl get_model {


    pub  async fn get_model(language_model: LanguageModel) -> Result<(), Err()> {
        let config = Config::init();
        let src="https://huggingface.co/ggerganov/whisper.cpp";
        let pfx = "resolve/main/ggml";
        //let tmp_dir = Builder::new().prefix("example").tempdir();

        //    wget --no-config --quiet --show-progress -O ggml-$model.bin $src/$pfx-$model.bin
        let target =  format!("{}/{}/{}",src,pfx,language_model.to_string());
        let response = reqwest::get(target).await?;

        let mut dest = {
            let fname = response
                .url()
                .path_segments()
                .and_then(|segments| segments.last())
                .and_then(|name| if name.is_empty() { None } else { Some(name) })
                .unwrap_or(config.data.path_config.model_path.as_str());
            //Logger::log!(1,format!("file to download: '{}'", fname));
            let fname = tmp_dir.path().join(fname);
            println!("will be located under: '{:?}'", fname);
            File::create(fname)?
        };
        let content =  response.text().await?;
        copy(&mut content.as_bytes(), &mut dest)
        //Ok(())
    }

}