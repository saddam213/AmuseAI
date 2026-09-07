using System.ComponentModel.DataAnnotations;

namespace Amuse.Common
{
    public enum ServerType
    {
        OnnxRuntime = 0,
        HuggingFace = 10,
        StableDiffusionCpp = 20
    }

    public enum PipelineType
    {
        // Image
        [Display(Name = "Stable-Diffusion Pipeline", ShortName = "SD", Description = "The original Stable Diffusion family, widely supported and highly customizable. Known for its large ecosystem of checkpoints, LoRAs, ControlNets, and community tooling, making it a versatile choice for general image generation and fine-tuned workflows.")]
        StableDiffusionPipeline = 0,

        [Display(Name = "Stable-Diffusion XL Pipeline", ShortName = "SDXL", Description = "A major evolution of Stable Diffusion with a larger architecture and improved text understanding, composition, and visual detail. Particularly well suited to high-resolution generation, realistic imagery, and complex prompts.")]
        StableDiffusionXLPipeline = 1,

        [Display(Name = "Stable-Diffusion 3 Pipeline", ShortName = "SD3", Description = "A newer Stable Diffusion architecture focused on stronger prompt comprehension, improved typography and text rendering, and better handling of complex compositions. Uses a substantially different architecture from earlier Stable Diffusion generations.")]
        StableDiffusion3Pipeline = 2,

        [Display(Name = "Latent Consistency Pipeline", ShortName = "LCM", Description = "Latent Consistency Models are designed to generate images in very few sampling steps rather than the many steps traditionally used by diffusion models. They prioritize speed and interactive generation while retaining reasonable image quality.")]
        LatentConsistencyPipeline = 3,

        [Display(Name = "FLUX.1 Pipeline", ShortName = "FLUX.1", Description = "The FLUX.1 family uses a large flow-based architecture designed for strong visual quality, prompt adherence, and complex scene composition. It is particularly notable for its ability to follow detailed prompts and produce more convincing text and typography than many earlier diffusion models.")]
        FluxPipeline = 20,

        [Display(Name = "FLUX.2 Pipeline", ShortName = "FLUX.2", Description = "The next generation of the FLUX family, targeting higher-quality image synthesis, improved instruction following, and stronger control over complex visual compositions. Designed for demanding image-generation workflows where semantic accuracy and visual fidelity are important.")]
        Flux2Pipeline = 21,

        [Display(Name = "FLUX.2 Klein Pipeline", ShortName = "FLUX.2", Description = "A compact FLUX.2 variant designed to make the capabilities of the FLUX family more accessible with substantially lower computational requirements. Best suited to applications where generation speed, memory usage, and local inference efficiency are important.")]
        Flux2KleinPipeline = 22,

        [Display(Name = "Chroma Pipeline", ShortName = "Chroma", Description = "Chroma is a Flux-derived text-to-image model focused on improving prompt understanding, image quality, and detailed visual composition.")]
        ChromaPipeline = 30,

        [Display(Name = "Z-Image Pipeline", ShortName = "Z-Image", Description = "Z-Image is Alibaba's efficient image-generation model built around a Scalable Single-Stream DiT architecture. It combines strong visual quality, prompt adherence, aesthetic diversity, and controllability, with Z-Image-Turbo providing an aggressively distilled 8-step variant optimized for very fast photorealistic generation and English/Chinese text rendering.")]
        ZImagePipeline = 40,

        [Display(Name = "Qwen Image Pipeline", ShortName = "Qwen", Description = "Qwen's image-generation family, with particular emphasis on instruction following, complex compositions, and text rendering. It is well suited to tasks where the model needs to understand detailed natural-language instructions rather than simply reproduce a visual style.")]
        QwenImagePipeline = 50,

        [Display(Name = "LongCat Image Pipeline", ShortName = "LongCat", Description = "LongCat-Image, a pioneering open-source and bilingual (Chinese-English) foundation model for image generation, designed to address core challenges in multilingual text rendering, photorealism, deployment efficiency, and developer accessibility prevalent in current leading models.")]
        LongCatImagePipeline = 57,

        [Display(Name = "Lens Pipeline", ShortName = "Lens", Description = "Microsoft's Lens is a compact 3.8-billion parameter text-to-image model designed for high-speed generation and precise prompt adherence. Built on a dual-stream Multimodal Diffusion Transformer (MMDiT), it bridges multi-layer text features from a large language model with a high-fidelity semantic VAE.")]
        LensPipeline = 58,

        [Display(Name = "HiDream Pipeline", ShortName = "HiDream", Description = "HiDream-O1 is an 8B-parameter unified image-generation model designed for text-to-image generation, instruction-based editing, long-text rendering, subject-driven personalization, and storyboard generation. It uses a Pixel-level Unified Transformer architecture with native support for complex prompts, detailed compositions.")]
        HiDreamPipeline = 59,

        [Display(Name = "Ideogram4 Pipeline", ShortName = "Ideogram4", Description = "A model family particularly strong at generating images containing readable, correctly structured text. Well suited to posters, advertisements, logos, typography-heavy designs, and other graphic-design-oriented generations where text accuracy matters.")]
        IdeogramPipeline = 61,

        [Display(Name = "Anima Pipeline", ShortName = "Anima", Description = "Anima is a 2B-parameter image-generation model specialized in anime, illustration, characters, and other non-photorealistic artwork. It is trained primarily on anime and artistic imagery, providing strong style adherence and diversity, with Base, Aesthetic, and Turbo variants targeting flexibility, quality, and fast generation respectively.")]
        AnimaPipeline = 62,

        [Display(Name = "Ernie Pipeline", ShortName = "Ernie", Description = "Baidu's ERNIE-based image-generation models, built around strong multimodal understanding and natural-language instruction following. Designed to translate complex semantic descriptions into coherent visual compositions.")]
        ErniePipeline = 63,

        [Display(Name = "GLM-Image Pipeline", ShortName = "GLM-Image", Description = "Hybrid autoregressive and diffusion image-generation model with strong semantic understanding, accurate text rendering, fine-grained detail generation, and support for image editing, style transfer, identity preservation, and multi-subject consistency")]
        GlmImagePipeline = 64,

        [Display(Name = "Krea2 Pipeline", ShortName = "Krea2", Description = "Krea's image-generation model family, oriented toward aesthetically strong, modern image generation. Designed with creative workflows in mind, particularly polished compositions, photorealistic imagery, and visually appealing outputs.")]
        Krea2Pipeline = 65,

        [Display(Name = "JoyAI Pipeline", ShortName = "JoyAI", Description = "JoyAI-Image is a unified multimodal model combining an 8B multimodal language model with a 16B multimodal diffusion transformer. Its key strengths are spatial understanding, long-form text rendering, multi-view generation, and instruction-guided image editing, allowing generation and editing to be driven by detailed spatial instructions.")]
        JoyImagePipeline = 66,

        [Display(Name = "PRX-Pixel Pipeline", ShortName = "PRX-Pixel", Description = "PRX-Pixel is a ~7B pixel-space text-to-image foundation model that generates directly in pixel space without a VAE. Built with a Qwen3-VL text encoder and trained from scratch using x-prediction, it explores direct pixel generation with strong multilingual prompting while avoiding the compressed latent representation used by conventional diffusion models.")]
        PrxPixelPipeline = 67,

        [Display(Name = "Kandinsky5 Image Pipeline", ShortName = "Kandinsky5", Description = "The fifth generation of the Kandinsky family, designed for high-quality text-to-image generation with strong semantic understanding and detailed visual synthesis. Intended as a general-purpose model for both artistic and realistic image generation.")]
        Kandinsky5ImagePipeline = 68,

        [Display(Name = "Boogu Image Pipeline", ShortName = "BooguImage", Description = "BooguImage is an AI-powered image generation and editing tool that helps users quickly turn ideas into high-quality visuals. With simple prompts, users can create, edit, enhance, and transform images for creative, personal, or professional projects.")]
        BooguImagePipeline = 69,


        // Video
        [Display(Name = "Wan Pipeline", ShortName = "Wan", Description = "A general-purpose video-generation family designed for high-quality text-to-video and image-to-video synthesis. Known for strong visual quality, relatively coherent motion, and broad applicability across cinematic and creative video-generation tasks.")]
        WanPipeline = 70,

        [Display(Name = "LTX Pipeline", ShortName = "LTX", Description = "A video-generation architecture optimized heavily for speed and efficiency. Its ability to generate video with relatively low latency makes it particularly useful for interactive workflows, rapid experimentation, and local inference.")]
        LTXPipeline = 80,

        [Display(Name = "LTX-2 Pipeline", ShortName = "LTX-2", Description = "The next generation of LTX, extending the original architecture with improved video quality, motion, temporal consistency, and multimodal generation capabilities. Designed to provide significantly stronger results while retaining the efficiency that distinguishes the LTX family.")]
        LTX20Pipeline = 81,

        [Display(Name = "CogVideoX Pipeline", ShortName = "CogVideoX", Description = "An open video-generation model family designed for text-to-video and image-to-video generation. It focuses on maintaining temporal consistency between frames while producing meaningful motion and preserving the semantic content of the prompt.")]
        CogVideoXPipeline = 90,

        [Display(Name = "Kandinsky5 Video Pipeline", ShortName = "Kandinsky5", Description = "A video-oriented extension of the Kandinsky family, combining the model family's visual synthesis capabilities with temporal generation. Designed for coherent video creation with detailed scenes and stylized visual output.")]
        Kandinsky5VideoPipeline = 91,

        [Display(Name = "SkyReels v2 Pipeline", ShortName = "SkyReels", Description = "A video-generation family focused on cinematic and high-quality visual generation. Designed for more sophisticated scene composition, character motion, camera movement, and temporal consistency across generated sequences.")]
        SkyReelsV2Pipeline = 100,

        [Display(Name = "Helios Pipeline", ShortName = "Helios", Description = "Helios is a 14B autoregressive diffusion model designed for real-time, minute-scale video generation. It natively supports text-to-video, image-to-video, and video-to-video generation, using compressed historical context and efficient sampling to achieve high throughput while maintaining temporal consistency over long sequences.")]
        HeliosPipeline = 110,

        [Display(Name = "Motif Video Pipeline", ShortName = "Motif", Description = "Motif-Video 2B is a 2B-parameter text-to-video and image-to-video diffusion transformer designed to achieve competitive video quality with a fraction of the training compute and model size of larger video models. Its architecture separates prompt alignment, temporal consistency, and fine-detail recovery to reduce objective interference during generation.")]
        MotifVideoPipeline = 111,

        [Display(Name = "AnyFlow Pipeline", ShortName = "AnyFlow", Description = "AnyFlow is an any-step video diffusion framework based on flow maps that allows a single model to operate across arbitrary inference step counts rather than being locked to a fixed distilled step budget. It supports text-to-video, image-to-video, and video-to-video generation while improving quality progressively as additional sampling steps are used.")]
        AnyFlowPipeline = 112,

        [Display(Name = "MiniMax Video Pipeline", ShortName = "MiniMax", Description = "MiniMax's video-generation models are designed for high-quality text-to-video and image-to-video generation, with particular emphasis on realistic motion, detailed scenes, and cinematic output. Suitable for longer-form creative video generation.")]
        MiniMaxVideoPipeline = 113,


        // Audio
        [Display(Name = "AceStep Pipeline", ShortName = "AceStep", Description = "An open music-generation model designed for controllable music synthesis from natural-language descriptions. Supports structured musical generation and is particularly useful for generating songs, instrumental pieces, and genre-specific compositions.")]
        AceStepPipeline = 200,

        [Display(Name = "LongCat Audio Pipeline", ShortName = "LongCat", Description = "A generative audio model designed for producing rich audio content from textual and multimodal instructions. Intended for more general audio-generation workflows beyond simple speech synthesis.")]
        LongCatAudioPipeline = 210,

        [Display(Name = "MiniMax Audio Pipeline", ShortName = "MiniMax", Description = "MiniMax's audio-generation models cover expressive generative audio and music tasks, with an emphasis on high-quality output and natural musical structure. Suitable for creating music and other complex audio content from prompts.")]
        MiniMaxAudioPipeline = 213,


        // Other
        [Display(Name = "Upscale Pipeline", ShortName = "Upscale", Description = "General-purpose image upscaling pipeline designed to increase resolution and enhance visual detail while preserving the original image content.")]
        UpscalePipeline = 500,

        [Display(Name = "Extract Pipeline", ShortName = "Extract", Description = "General-purpose feature extraction pipeline for extracting useful representations, embeddings, metadata, or other features from supported inputs.")]
        ExtractPipeline = 501,

        [Display(Name = "Whisper Pipeline", ShortName = "Whisper", Description = "OpenAI's Whisper family is designed for robust automatic speech recognition across languages, accents, and noisy recording conditions. It can transcribe spoken audio into text and is particularly useful for multilingual transcription and speech-to-text workflows.")]
        WhisperPipeline = 502,

        [Display(Name = "Supertonic Pipeline", ShortName = "Supertonic", Description = "A lightweight text-to-speech model designed to convert text into natural-sounding speech efficiently. Focuses on practical local inference while providing expressive synthesized voices without requiring a large cloud-based speech service.")]
        SupertonicPipeline = 503,


        // Text
        [Display(Name = "AutoText Pipeline", ShortName = "AutoText", Description = "A general-purpose text-processing pipeline that automatically selects or orchestrates the appropriate text-generation capabilities for a requested operation. Intended as a higher-level abstraction for applications that should not need to know which underlying language model performs the task.")]
        AutoTextPipeline = 600,

        [Display(Name = "Qwen3 Pipeline", ShortName = "Qwen3", Description = "Qwen3 is Alibaba's next-generation open-weight language model family, designed for strong reasoning, instruction following, coding, multilingual understanding, and general-purpose text generation. It supports both efficient everyday tasks and more complex reasoning workflows, making it suitable for conversational AI, analysis, coding, and agentic applications.")]
        Qwen3Pipeline = 601,

        [Display(Name = "Gemma4 Pipeline", ShortName = "Gemma4", Description = "Gemma 4 is Google's latest open model family designed for efficient, high-quality text and multimodal generation. It provides strong instruction following, reasoning, coding, and general-purpose language capabilities while offering model sizes suitable for both local and resource-constrained inference.")]
        Gemma4Pipeline = 610,


        // Deprecated
        Kandinsky5Pipeline = 60,
    }

    public enum ProcessType
    {
        // Image
        [Display(Name = "TextToImage", ShortName = "T2I", Description = "Generates a brand new synthetic image completely from scratch based on a text prompt.")]
        TextToImage = 0,

        [Display(Name = "ImageToImage", ShortName = "I2I", Description = "Alters a source image changing its style, textures, or composition based on a guiding text prompt.")]
        ImageToImage = 1,

        [Display(Name = "ImageEdit", ShortName = "IE", Description = "Applies localized or structural modifications to an existing image using instruction-based editing commands.")]
        ImageEdit = 2,

        [Display(Name = "ImageInpaint", ShortName = "INP", Description = "Modifies or restores specific, masked areas within an image while preserving the surrounding context.")]
        ImageInpaint = 3,

        [Display(Name = "ControlNet Image", ShortName = "CN", Description = "Applies rigid spatial conditioning (like edge maps, poses, or depth) onto a text-to-image generation process.")]
        ImageControlNet = 4,

        [Display(Name = "ControlNet ImageToImage", ShortName = "I2I+CN", Description = "Combines a source image with an explicit spatial guide map to tightly control composition and style concurrently.")]
        ImageToImageControlNet = 5,


        // Video
        [Display(Name = "TextToVideo", ShortName = "T2V", Description = "Synthesizes fluid, moving video frames from scratch using a conceptual text prompt.")]
        TextToVideo = 300,

        [Display(Name = "ImageToVideo", ShortName = "I2V", Description = "Animates a single, static source image into a moving video clip while maintaining character or object consistency.")]
        ImageToVideo = 301,

        [Display(Name = "VideoToVideo", ShortName = "V2V", Description = "Translates a source video into a different style or texture while tracking the underlying motion structures.")]
        VideoToVideo = 302,


        // Audio
        [Display(Name = "TextToAudio", ShortName = "T2A", Description = "Converts written text into spoken voice synthesis, realistic sound effects, or continuous music tracks.")]
        TextToAudio = 400,

        [Display(Name = "AudioToText", ShortName = "A2T", Description = "Transcribes incoming spoken speech or environmental audio signals into formatted, written text.")]
        AudioToText = 500,


        // Text
        [Display(Name = "TextToText", ShortName = "T2T", Description = "")]
        TextToText = 800,

        [Display(Name = "ImageToText", ShortName = "I2T", Description = "")]
        ImageToText = 801
    }

    public enum EnvironmentMode
    {
        Create = 0,
        Load = 1,
        Update = 2,
        Rebuild = 3,
        Reinstall = 4
    }

    public enum DataType
    {
        Float32 = 0,
        Bfloat16 = 1,
        Float16 = 2,
        Float8 = 3,
        Int8 = 6,
        Int4 = 7
    }

    public enum QuantizationType
    {
        Q16Bit = 0,
        Q8Bit = 1,
        Q4Bit = 2
    }

    public enum MemoryModeType
    {
        Device = 0,
        OffloadCPU = 1,
        OffloadModel = 2,
        Balanced = 3
    }

    public enum SchedulerType
    {
        [Display(Name = "LMS")]
        LMS = 0,

        [Display(Name = "Euler")]
        Euler = 1,

        [Display(Name = "Euler Ancestral")]
        EulerAncestral = 2,

        [Display(Name = "DDPM")]
        DDPM = 3,

        [Display(Name = "DDIM")]
        DDIM = 4,

        [Display(Name = "KDPM2")]
        KDPM2 = 5,

        [Display(Name = "KDPM2-Ancestral")]
        KDPM2Ancestral = 6,

        [Display(Name = "DDPM-Wuerstchen")]
        DDPMWuerstchen = 10,

        [Display(Name = "LCM")]
        LCM = 20,

        [Display(Name = "FlowMatch-Euler")]
        FlowMatchEuler = 30,

        [Display(Name = "FlowMatch-Heun")]
        FlowMatchHeun = 31,

        [Display(Name = "PNDM")]
        PNDM = 40,

        [Display(Name = "Heun")]
        Heun = 41,

        [Display(Name = "UniPC Multistep")]
        UniPCMultistep = 42,

        [Display(Name = "DPM Solver Multistep")]
        DPMSolverMultistep = 43,

        [Display(Name = "DPM Single Step")]
        DPMSolverSinglestep = 45,

        [Display(Name = "DPM Solver SDE")]
        DPMSolverSDE = 46,

        [Display(Name = "DEIS Multistep")]
        DEISMultistep = 47,

        [Display(Name = "EDM Euler")]
        EDMEuler = 48,

        [Display(Name = "EDM DPM Solver Multistep")]
        EDMDPMSolverMultistep = 49,

        [Display(Name = "FlowMatch-LCM")]
        FlowMatchLCM = 50,

        [Display(Name = "IPNDM")]
        IPNDM = 51,

        [Display(Name = "CogVideoX DDIM")]
        CogVideoXDDIM = 52,

        [Display(Name = "CogVideoX DPM")]
        CogVideoXDPM = 53,

        [Display(Name = "Helios")]
        Helios = 54,

        [Display(Name = "Helios DMD")]
        HeliosDMD = 55,

        [Display(Name = "TCD")]
        TCD = 56,

        [Display(Name = "SCM")]
        SCM = 57,

        [Display(Name = "SA Solver")]
        SASolver = 58,

        [Display(Name = "LTX Euler Ancestral")]
        LTXEulerAncestral = 59,

        [Display(Name = "DPM2")]
        DPM2 = 60,

        [Display(Name = "DPM++ 2S Ancestral")]
        DPMPlusPlus2SAncestral = 61,

        [Display(Name = "DPM++ 2M")]
        DPMPlusPlus2M = 62,

        [Display(Name = "DPM++ 2M v2")]
        DPMPlusPlus2Mv2 = 63,

        [Display(Name = "DPM++ 2M SDE")]
        DPMPlusPlus2MSDE = 64,

        [Display(Name = "DPM++ 2M SDE BT")]
        DPMPlusPlus2MSDEBT = 65,

        [Display(Name = "Residual Multistep")]
        ResidualMultistep = 66,

        [Display(Name = "Residual 2S")]
        Residual2S = 67,

        [Display(Name = "ER-SDE")]
        ERSDE = 68,
    }


    public enum SigmaScheduleType
    {
        [Display(Name = "Default")]
        Default = 0,

        [Display(Name = "Discrete")]
        Discrete = 1,

        [Display(Name = "Normal")]
        Normal = 2,

        [Display(Name = "Karras")]
        Karras = 3,

        [Display(Name = "Exponential")]
        Exponential = 4,

        [Display(Name = "AYS")]
        AYS = 5,

        [Display(Name = "GITS")]
        GITS = 6,

        [Display(Name = "SGMUniform")]
        SGMUniform = 7,

        [Display(Name = "Simple")]
        Simple = 8,

        [Display(Name = "Smoothstep")]
        Smoothstep = 9,

        [Display(Name = "KLOptimal")]
        KLOptimal = 10,

        [Display(Name = "LCM")]
        LCM = 11,

        [Display(Name = "BongTangent")]
        BongTangent = 12,

        [Display(Name = "LTX2")]
        LTX2 = 13,

        [Display(Name = "LogitNormal")]
        LogitNormal = 14,

        [Display(Name = "Flux")]
        Flux = 15,

        [Display(Name = "Flux2")]
        Flux2 = 16,

        [Display(Name = "Beta")]
        Beta = 17,
    }

    public enum TimestepSpacingType
    {
        Leading = 0,
        Trailing = 1,
        Linspace = 2
    }

    public enum AlgorithmType
    {
        DPMSolver = 0,
        DPMSolverPlus = 1,
        SDE_DPMSolver = 2,
        SDE_DPMSolverPlus = 3,
        DEIS = 4,
        DataPrediction = 5,
        NoisePrediction = 6
    }

    public enum SolverType
    {
        Midpoint = 0,
        Heun = 1,
        BH1 = 2,
        BH2 = 3,
        LogRho = 4
    }

    public enum BetaScheduleType
    {
        Linear = 0,
        ScaledLinear = 1,
        Cosine = 2,
        SquaredCosine = 3,
        Sigmoid = 4,
        Laplace = 5,
        Exponential = 6
    }

    public enum PredictionType
    {
        Epsilon = 0,
        Variable = 1,
        Sample = 2,
        FlowPrediction = 3,
        Trigflow = 4
    }

    public enum VarianceType
    {
        FixedSmall = 0,
        FixedSmallLog = 1,
        FixedLarge = 2,
        FixedLargeLog = 3,
        Learned = 4,
        LearnedRange = 5
    }

    public enum TimeShiftType
    {
        Linear = 0,
        Exponential = 1
    }

    public enum AlphaTransformType
    {
        Cosine = 0,
        Exponential = 1,
        Laplace = 2
    }

    public enum FinalSigmasType
    {
        Zero = 0,
        SigmaMin = 1
    }

    public enum InterpolationType
    {
        Linear = 0,
        LogLinear = 1
    }

    public enum TimestepType
    {
        Discrete = 0,
        Continuous = 1
    }

    public enum UpscaleModeType
    {
        Nearest = 0,
        Linear = 1,
        Bilinear = 2,
        Bicubic = 3,
        Trilinear = 4,
        Area = 5,
        NearestExact = 6
    }


    public enum LanguageType
    {
        [Display(Name = "None", ShortName = "")]
        None = 0,

        [Display(Name = "Auto", ShortName = "en")]
        Auto = 1,

        [Display(Name = "Afrikaans", ShortName = "af")]
        Afrikaans = 2,

        [Display(Name = "Albanian", ShortName = "sq")]
        Albanian = 3,

        [Display(Name = "Amharic", ShortName = "am")]
        Amharic = 4,

        [Display(Name = "Arabic", ShortName = "ar")]
        Arabic = 5,

        [Display(Name = "Armenian", ShortName = "hy")]
        Armenian = 6,

        [Display(Name = "Assamese", ShortName = "as")]
        Assamese = 7,

        [Display(Name = "Azerbaijani", ShortName = "az")]
        Azerbaijani = 8,

        [Display(Name = "Bashkir", ShortName = "ba")]
        Bashkir = 9,

        [Display(Name = "Basque", ShortName = "eu")]
        Basque = 10,

        [Display(Name = "Belarusian", ShortName = "be")]
        Belarusian = 11,

        [Display(Name = "Bengali", ShortName = "bn")]
        Bengali = 12,

        [Display(Name = "Bosnian", ShortName = "bs")]
        Bosnian = 13,

        [Display(Name = "Breton", ShortName = "br")]
        Breton = 14,

        [Display(Name = "Bulgarian", ShortName = "bg")]
        Bulgarian = 15,

        [Display(Name = "Burmese", ShortName = "my")]
        Burmese = 16,

        [Display(Name = "Catalan", ShortName = "ca")]
        Catalan = 17,

        [Display(Name = "Chinese", ShortName = "zh")]
        Chinese = 18,

        [Display(Name = "Croatian", ShortName = "hr")]
        Croatian = 19,

        [Display(Name = "Czech", ShortName = "cs")]
        Czech = 20,

        [Display(Name = "Danish", ShortName = "da")]
        Danish = 21,

        [Display(Name = "Dutch", ShortName = "nl")]
        Dutch = 22,

        [Display(Name = "English", ShortName = "en")]
        English = 23,

        [Display(Name = "Estonian", ShortName = "et")]
        Estonian = 24,

        [Display(Name = "Faroese", ShortName = "fo")]
        Faroese = 25,

        [Display(Name = "Finnish", ShortName = "fi")]
        Finnish = 26,

        [Display(Name = "French", ShortName = "fr")]
        French = 27,

        [Display(Name = "Galician", ShortName = "gl")]
        Galician = 28,

        [Display(Name = "Georgian", ShortName = "ka")]
        Georgian = 29,

        [Display(Name = "German", ShortName = "de")]
        German = 30,

        [Display(Name = "Greek", ShortName = "el")]
        Greek = 31,

        [Display(Name = "Gujarati", ShortName = "gu")]
        Gujarati = 32,

        [Display(Name = "Haitian", ShortName = "ht")]
        Haitian = 33,

        [Display(Name = "Hausa", ShortName = "ha")]
        Hausa = 34,

        [Display(Name = "Hawaiian", ShortName = "haw")]
        Hawaiian = 35,

        [Display(Name = "Hebrew", ShortName = "he")]
        Hebrew = 36,

        [Display(Name = "Hindi", ShortName = "hi")]
        Hindi = 37,

        [Display(Name = "Hungarian", ShortName = "hu")]
        Hungarian = 38,

        [Display(Name = "Icelandic", ShortName = "is")]
        Icelandic = 39,

        [Display(Name = "Indonesian", ShortName = "id")]
        Indonesian = 40,

        [Display(Name = "Italian", ShortName = "it")]
        Italian = 41,

        [Display(Name = "Japanese", ShortName = "ja")]
        Japanese = 42,

        [Display(Name = "Javanese", ShortName = "jw")]
        Javanese = 43,

        [Display(Name = "Kannada", ShortName = "kn")]
        Kannada = 44,

        [Display(Name = "Kazakh", ShortName = "kk")]
        Kazakh = 45,

        [Display(Name = "Khmer", ShortName = "km")]
        Khmer = 46,

        [Display(Name = "Korean", ShortName = "ko")]
        Korean = 47,

        [Display(Name = "Lao", ShortName = "lo")]
        Lao = 48,

        [Display(Name = "Latin", ShortName = "la")]
        Latin = 49,

        [Display(Name = "Latvian", ShortName = "lv")]
        Latvian = 50,

        [Display(Name = "Lingala", ShortName = "ln")]
        Lingala = 51,

        [Display(Name = "Lithuanian", ShortName = "lt")]
        Lithuanian = 52,

        [Display(Name = "Luxembourgish", ShortName = "lb")]
        Luxembourgish = 53,

        [Display(Name = "Macedonian", ShortName = "mk")]
        Macedonian = 54,

        [Display(Name = "Malagasy", ShortName = "mg")]
        Malagasy = 55,

        [Display(Name = "Malay", ShortName = "ms")]
        Malay = 56,

        [Display(Name = "Malayalam", ShortName = "ml")]
        Malayalam = 57,

        [Display(Name = "Maltese", ShortName = "mt")]
        Maltese = 58,

        [Display(Name = "Maori", ShortName = "mi")]
        Maori = 59,

        [Display(Name = "Marathi", ShortName = "mr")]
        Marathi = 60,

        [Display(Name = "Mongolian", ShortName = "mn")]
        Mongolian = 61,

        [Display(Name = "Nepali", ShortName = "ne")]
        Nepali = 62,

        [Display(Name = "Norwegian", ShortName = "no")]
        Norwegian = 63,

        [Display(Name = "Norwegian Nynorsk", ShortName = "nn")]
        NorwegianNynorsk = 64,

        [Display(Name = "Occitan", ShortName = "oc")]
        Occitan = 65,

        [Display(Name = "Persian", ShortName = "fa")]
        Persian = 66,

        [Display(Name = "Polish", ShortName = "pl")]
        Polish = 67,

        [Display(Name = "Portuguese", ShortName = "pt")]
        Portuguese = 68,

        [Display(Name = "Punjabi", ShortName = "pa")]
        Punjabi = 69,

        [Display(Name = "Romanian", ShortName = "ro")]
        Romanian = 70,

        [Display(Name = "Russian", ShortName = "ru")]
        Russian = 71,

        [Display(Name = "Sanskrit", ShortName = "sa")]
        Sanskrit = 72,

        [Display(Name = "Serbian", ShortName = "sr")]
        Serbian = 73,

        [Display(Name = "Shona", ShortName = "sn")]
        Shona = 74,

        [Display(Name = "Sindhi", ShortName = "sd")]
        Sindhi = 75,

        [Display(Name = "Sinhala", ShortName = "si")]
        Sinhala = 76,

        [Display(Name = "Slovak", ShortName = "sk")]
        Slovak = 77,

        [Display(Name = "Slovenian", ShortName = "sl")]
        Slovenian = 78,

        [Display(Name = "Somali", ShortName = "so")]
        Somali = 79,

        [Display(Name = "Spanish", ShortName = "es")]
        Spanish = 80,

        [Display(Name = "Sundanese", ShortName = "su")]
        Sundanese = 81,

        [Display(Name = "Swahili", ShortName = "sw")]
        Swahili = 82,

        [Display(Name = "Swedish", ShortName = "sv")]
        Swedish = 83,

        [Display(Name = "Tagalog", ShortName = "tl")]
        Tagalog = 84,

        [Display(Name = "Tajik", ShortName = "tg")]
        Tajik = 85,

        [Display(Name = "Tamil", ShortName = "ta")]
        Tamil = 86,

        [Display(Name = "Tatar", ShortName = "tt")]
        Tatar = 87,

        [Display(Name = "Telugu", ShortName = "te")]
        Telugu = 88,

        [Display(Name = "Thai", ShortName = "th")]
        Thai = 89,

        [Display(Name = "Tibetan", ShortName = "bo")]
        Tibetan = 90,

        [Display(Name = "Turkish", ShortName = "tr")]
        Turkish = 91,

        [Display(Name = "Turkmen", ShortName = "tk")]
        Turkmen = 92,

        [Display(Name = "Ukrainian", ShortName = "uk")]
        Ukrainian = 93,

        [Display(Name = "Urdu", ShortName = "ur")]
        Urdu = 94,

        [Display(Name = "Uzbek", ShortName = "uz")]
        Uzbek = 95,

        [Display(Name = "Vietnamese", ShortName = "vi")]
        Vietnamese = 96,

        [Display(Name = "Welsh", ShortName = "cy")]
        Welsh = 97,

        [Display(Name = "Yiddish", ShortName = "yi")]
        Yiddish = 98,

        [Display(Name = "Yoruba", ShortName = "yo")]
        Yoruba = 99
    }


    public enum ConversationRole
    {
        User = 0,
        System = 1,
        Assistant = 2
    }


    public enum CacheType
    {
        Dynamic = 0,
        DynamicOffload = 1,
        Static = 2,
        StaticOffload = 3,
        Quantized = 4,
        Disabled = 100,
    }


    public enum LatentUpscale
    {
        [Display(Name = "None")]
        None,

        [Display(Name = "Model")]
        Model,

        [Display(Name = "Latent")]
        Latent,

        [Display(Name = "Latent (nearest)")]
        LatentNearest,

        [Display(Name = "Latent (nearest-exact)")]
        LatentNearestExact,

        [Display(Name = "Latent (antialiased)")]
        LatentAntialiased,

        [Display(Name = "Latent (bicubic)")]
        LatentBicubic,

        [Display(Name = "Latent (bicubic antialiased)")]
        LatentBicubicAntialiased,

        [Display(Name = "Lanczos")]
        Lanczos,

        [Display(Name = "Nearest")]
        Nearest
    }
}
