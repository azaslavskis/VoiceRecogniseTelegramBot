use std::io::Cursor;

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug)]
struct TelegramResponse {
    ok: bool,
    result: TelegramResult,
}

#[derive(Serialize, Deserialize, Debug)]
struct TelegramResult {
    file_id: String,
    file_unique_id: String,
    file_size: i32,
    file_path: String,
}

pub fn get_file_file(file_id: String, TOKEN: String) {
    let url_to_download = format!(
        "https://api.telegram.org/bot{}/getFile?file_id={}",
        TOKEN, file_id
    );
    let resp = reqwest::blocking::get(url_to_download).unwrap();
    let response: TelegramResponse = serde_json::from_str(resp.text().unwrap().as_str()).unwrap();
    let file_path = response.result.file_path;
    let url_to_download = format!("https://api.telegram.org/file/bot{}/{}", TOKEN, file_path);
    let response = reqwest::blocking::get(url_to_download.clone()).unwrap();
    println!("{}", url_to_download.clone());
    let mut file = std::fs::File::create("file.oga").unwrap();
    let mut content = Cursor::new(response.bytes().unwrap());
    std::io::copy(&mut content, &mut file).unwrap();
}
