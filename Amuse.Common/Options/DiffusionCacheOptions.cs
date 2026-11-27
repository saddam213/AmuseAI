namespace Amuse.Common
{
    public sealed record DiffusionCacheOptions
    {
        public DiffusionCacheType Mode { get; set; }
        public float Threshold { get; set; }
        public float StartPercent { get; set; }
        public float EndPercent { get; set; }
        public int WarmupSteps { get; set; }
    }
}
