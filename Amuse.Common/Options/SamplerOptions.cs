namespace Amuse.Common
{
    public sealed record SamplerOptions
    {
        public SamplerType Sampler { get; set; }
        public SchedulerType Scheduler { get; set; }
        public float FlowShift { get; set; }
        public float Eta { get; set; }
        public float ImageGuidance { get; set; }
        public int ShiftedTimestep { get; set; }
        public float[] Sigmas { get; set; }
        public string ExtraArgs { get; set; }
        public int[] SkipLayers { get; set; }
        public float SkipLayerScale { get; set; }
        public float SkipLayerStart { get; set; }
        public float SkipLayerEnd { get; set; }
    }
}
