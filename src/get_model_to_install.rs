use crate::config_parsing::Config;
use crate::language_model_declaration::LanguageModel;
use crate::Logger;
use std::io::Cursor;
use tokio::runtime::Runtime;
use std::process::exit;
type Result<T> = std::result::Result<T, Box<dyn std::error::Error + Send + Sync>>;

pub struct get_model {}

impl get_model {
    async fn fetch_url(url: String, file_name: String) -> Result<()> {
        let response = reqwest::get(url).await?;
        let mut file = std::fs::File::create(file_name)?;
        let mut content = Cursor::new(response.bytes().await?);
        std::io::copy(&mut content, &mut file)?;
        Ok(())
    }

    pub fn get_model(language_model: LanguageModel) {
        let config = Config::init();
        let model_path = config.data.path_config.model_path;
        let url = format!(
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{}.bin?download=true",
            language_model.to_string()

        );
        Logger::log(1, &url);
        let rt = Runtime::new().unwrap();
        let future = Self::fetch_url(url, model_path);
        let r = rt.block_on(future);
        match r {
            Err(r) => {
                let message = format!("cloning model failed: {} ",&r.to_string());
                Logger::log(4,message.as_str());
            }
            Ok(r) => ()
        }
    }
}
