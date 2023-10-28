mod get_file;
mod recognise_message;
static TOKEN: &str = "";
static CHAT_ID: i64 = 0;

use frankenstein::GetUpdatesParams;
use frankenstein::SendMessageParams;
use frankenstein::TelegramApi;
use frankenstein::{Api, UpdateContent};
use recognise_message::init_recognise;

use crate::get_file::get_file_file;


fn to_string(string:Vec<String>) -> String {
    let mut long_string=String::new();
    for str in string.into_iter(){
        long_string=long_string+&str+"\n";
        
    }
    return long_string;
    
}

fn main() {
    let api = Api::new(TOKEN);

    let update_params_builder = GetUpdatesParams::builder();
    let mut update_params = update_params_builder.clone().build();

    loop {
        let result = api.get_updates(&update_params);

        //println!("result: {result:?}");

        match result {
            Ok(response) => {
                for update in response.result {
                    if let UpdateContent::Message(message) = update.content {
                        let message_is_audio = message.voice;
                        if message_is_audio.is_some() {
                            let file_id = message_is_audio.unwrap().as_mut().file_id.clone();
                            get_file_file(file_id, TOKEN.to_string());
                            let send_message_state = SendMessageParams::builder()
                                .chat_id(message.chat.id)
                                .text("голосовое в скачано")
                                .build();
                            if let Err(err) = api.send_message(&send_message_state) {
                                println!("Failed to send message: {err:?}");
                            }

                          

                            let text=init_recognise();

                            for str in text.into_iter(){
                            let send_message_params = SendMessageParams::builder()
                            .chat_id(message.chat.id)
                            .text(str)
                            .reply_to_message_id(message.message_id)
                            .build();
                        if let Err(err) = api.send_message(&send_message_params) {
                            println!("Failed to send message: {err:?}");
                        }
                            }

                            
                        }
                    }
                    update_params = update_params_builder
                        .clone()
                        .offset(update.update_id + 1)
                        .build();
                }
            }
            Err(error) => {
                println!("Failed to get updates: {error:?}");
            }
        }
    }
}
