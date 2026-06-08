using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum PipelineType
    {
        // Image
        [Display(Name = "Stable-Diffusion Pipeline", ShortName = "SD")]
        StableDiffusionPipeline = 0,

        [Display(Name = "Stable-Diffusion XL Pipeline", ShortName = "SDXL")]
        StableDiffusionXLPipeline = 1,

        [Display(Name = "Stable-Diffusion 3 Pipeline", ShortName = "SD3")]
        StableDiffusion3Pipeline = 2,

        [Display(Name = "Latent Consistency Pipeline", ShortName = "LCM")]
        LatentConsistencyPipeline = 3,

        [Display(Name = "FLUX.1 Pipeline", ShortName = "FLUX.1")]
        FluxPipeline = 20,

        [Display(Name = "FLUX.2 Pipeline", ShortName = "FLUX.2")]
        Flux2Pipeline = 21,

        [Display(Name = "FLUX.2 Klein Pipeline", ShortName = "FLUX.2")]
        Flux2KleinPipeline = 22,

        [Display(Name = "Chroma Pipeline", ShortName = "Chroma")]
        ChromaPipeline = 30,

        [Display(Name = "Z-Image Pipeline", ShortName = "Z-Image")]
        ZImagePipeline = 40,

        [Display(Name = "Qwen Image Pipeline", ShortName = "Qwen")]
        QwenImagePipeline = 50,

        [Display(Name = "Kandinsky5 Pipeline", ShortName = "Kandinsky5")]
        Kandinsky5Pipeline = 60,


        [Display(Name = "Anima Pipeline", ShortName = "Anima")]
        AnimaPipeline = 62,


        // Video
        [Display(Name = "Wan Pipeline", ShortName = "Wan")]
        WanPipeline = 70,

        [Display(Name = "LTX Pipeline", ShortName = "LTX")]
        LTXPipeline = 80,

        [Display(Name = "LTX-2 Pipeline", ShortName = "LTX-2")]
        LTX20Pipeline = 81,

        [Display(Name = "CogVideoX Pipeline", ShortName = "CogVideoX")]
        CogVideoXPipeline = 90,

        [Display(Name = "SkyReels v2 Pipeline", ShortName = "SkyReels")]
        SkyReelsV2Pipeline = 100,

        [Display(Name = "Helios Pipeline", ShortName = "Helios")]
        HeliosPipeline = 110,


        // Audio
        [Display(Name = "AceStep Pipeline", ShortName = "AceStep")]
        AceStepPipeline = 200,

        [Display(Name = "LongCat Audio Pipeline", ShortName = "LongCat")]
        LongCatAudioPipeline = 210,

        // Other
        [Display(Name = "Upscale Pipeline", ShortName = "Upscale")]
        UpscalePipeline = 500,

        [Display(Name = "Extract Pipeline", ShortName = "Extract")]
        ExtractPipeline = 501,

        [Display(Name = "Whisper Pipeline", ShortName = "Whisper")]
        WhisperPipeline = 502,

        [Display(Name = "Supertonic Pipeline", ShortName = "Supertonic")]
        SupertonicPipeline = 503,
    }
}
