use std::fs::File;

use reqwest::header;
use whisper_rs::{FullParams, SamplingStrategy, WhisperContext};

pub fn init_recognise() -> Vec<String> {
    let mut f = File::open("file.oga").unwrap();
    let (raw, header) = ogg_opus::decode::<_, 16000>(f).unwrap();
    let mut ctx = WhisperContext::new("./ggml-tiny.bin").expect("failed to load model");
    
    //let original_samples = parse_wav_file(audio_path);
    let samples = whisper_rs::convert_integer_to_float_audio(&raw);

    let mut  params = FullParams::new(SamplingStrategy::default());
    params.set_translate(false);
    // Set the language to translate to to English.
    params.set_language(Some("ru"));
    params.set_print_realtime(true);
    ctx.full(params, &samples)
        .expect("failed to convert samples");
   
    let num_segments = ctx.full_n_segments();
    let mut vector:Vec<String>= vec![String::new();num_segments as usize ];
    for i in 0..num_segments {
        let segment = ctx.full_get_segment_text(i).expect("failed to get segment");
        let start_timestamp = ctx.full_get_segment_t0(i);
        let end_timestamp = ctx.full_get_segment_t1(i);
        //println!("[{} - {}]: {}", start_timestamp, end_timestamp, segment);
        let message=format!("[{} - {}]: {}", start_timestamp, end_timestamp, segment);
        vector.push(message);
    }
    return vector;
}
