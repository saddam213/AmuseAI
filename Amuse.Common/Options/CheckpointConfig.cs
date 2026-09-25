namespace Amuse.Common
{
    public sealed record CheckpointConfig
    {
        public string Compute { get; set; }
        public string Tokenizer { get; set; }
        public string TextEncoder { get; set; }
        public string TextEncoder2 { get; set; }
        public string TextEncoder3 { get; set; }
        public string Unet { get; set; }
        public string Transformer { get; set; }
        public string Transformer2 { get; set; }
        public string Vae { get; set; }
        public string AudioVae { get; set; }
        public string Vocoder { get; set; }
        public string Connectors { get; set; }
        public string LatentUpsampler { get; set; }
        public string LatentUpsamplerTemporal { get; set; }
        public string ConditionEncoder { get; set; }
        public string AudioTokenizer { get; set; }
        public string AudioDetokenizer { get; set; }

        public string FullCheckpoint
        {
            get
            {
                if ((!string.IsNullOrEmpty(Unet) || !string.IsNullOrEmpty(Transformer))
                    && string.IsNullOrEmpty(Compute)
                    && string.IsNullOrEmpty(Tokenizer)
                    && string.IsNullOrEmpty(TextEncoder)
                    && string.IsNullOrEmpty(TextEncoder2)
                    && string.IsNullOrEmpty(TextEncoder3)
                    && string.IsNullOrEmpty(Vae)
                    && string.IsNullOrEmpty(AudioVae)
                    && string.IsNullOrEmpty(Vocoder)
                    && string.IsNullOrEmpty(LatentUpsampler)
                    && string.IsNullOrEmpty(LatentUpsamplerTemporal)
                    && string.IsNullOrEmpty(ConditionEncoder)
                    && string.IsNullOrEmpty(AudioTokenizer)
                    && string.IsNullOrEmpty(AudioDetokenizer))
                    return Unet ?? Transformer;

                return null;
            }
        }
    }
}
