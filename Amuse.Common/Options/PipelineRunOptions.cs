namespace Amuse.Common
{
    public sealed record PipelineRunOptions
    {
        public GenerateImageOptions ImageOptions { get; set; }
        public GenerateVideoOptions VideoOptions { get; set; }
        public GenerateAudioOptions AudioOptions { get; set; }
        public GenerateTextOptions TextOptions { get; set; }
        public UpscaleImageRunOptions UpscaleImageOptions { get; set; }
        public UpscaleVideoRunOptions UpscaleVideoOptions { get; set; }
        public ExtractImageRunOptions ExtractImageOptions { get; set; }
        public InterpolateVideoRunOptions InterpolateVideoOptions { get; set; }
    }
}
