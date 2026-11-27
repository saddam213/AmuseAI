namespace Amuse.App.Common
{
    /// <summary>
    /// The names of the fixed folders of the model directory.
    /// The folders that other applications also have use the ComfyUI names, so a model directory can be shared with them.
    /// </summary>
    public static class ModelFolders
    {
        public const string Diffusion = "diffusion_models";
        public const string TextEncoder = "text_encoders";
        public const string LLM = "llm";
        public const string AutoEncoder = "vae";
        public const string LoraAdapter = "loras";
        public const string ControlNet = "controlnet";
        public const string Upscale = "upscale_models";
        public const string Extract = "extractors";
        public const string Tokenizer = "tokenizers";
        public const string Components = "components";


        /// <summary>
        /// Gets the folder name of a model folder.
        /// </summary>
        /// <param name="folder">The model folder.</param>
        public static string GetName(ModelFolderType folder)
        {
            return folder switch
            {
                ModelFolderType.Diffusion => Diffusion,
                ModelFolderType.TextEncoder => TextEncoder,
                ModelFolderType.LLM => LLM,
                ModelFolderType.AutoEncoder => AutoEncoder,
                ModelFolderType.LoraAdapter => LoraAdapter,
                ModelFolderType.ControlNet => ControlNet,
                ModelFolderType.Upscale => Upscale,
                ModelFolderType.Extract => Extract,
                ModelFolderType.Tokenizer => Tokenizer,
                ModelFolderType.Components => Components,
                _ => folder.ToString()
            };
        }
    }
}
