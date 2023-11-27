#[derive(Debug,Copy, Clone)]
pub enum LanguageModel {
    TinyEn,
    Tiny,
    TinyQ51,
    TinyEnQ51,
    BaseEn,
    Base,
    BaseQ51,
    BaseEnQ51,
    SmallEn,
    SmallEnTdrz,
    Small,
    SmallQ51,
    SmallEnQ51,
    Medium,
    MediumEn,
    MediumQ50,
    MediumEnQ50,
    LargeV1,
    LargeV2,
    LargeV3,
    LargeQ50,
}

impl LanguageModel {
    pub fn to_string(&self) -> &'static str {
        match self {
            LanguageModel::TinyEn => "tiny.en",
            LanguageModel::Tiny => "tiny",
            LanguageModel::TinyQ51 => "tiny-q5_1",
            LanguageModel::TinyEnQ51 => "tiny.en-q5_1",
            LanguageModel::BaseEn => "base.en",
            LanguageModel::Base => "base",
            LanguageModel::BaseQ51 => "base-q5_1",
            LanguageModel::BaseEnQ51 => "base.en-q5_1",
            LanguageModel::SmallEn => "small.en",
            LanguageModel::SmallEnTdrz => "small.en-tdrz",
            LanguageModel::Small => "small",
            LanguageModel::SmallQ51 => "small-q5_1",
            LanguageModel::SmallEnQ51 => "small.en-q5_1",
            LanguageModel::Medium => "medium",
            LanguageModel::MediumEn => "medium.en",
            LanguageModel::MediumQ50 => "medium-q5_0",
            LanguageModel::MediumEnQ50 => "medium.en-q5_0",
            LanguageModel::LargeV1 => "large-v1",
            LanguageModel::LargeV2 => "large-v2",
            LanguageModel::LargeV3 => "large-v3",
            LanguageModel::LargeQ50 => "large-q5_0",
        }
    }
}
