namespace Amuse.App.Common
{
    /// <summary>
    /// The kind of pipeline that loaded a model, a diffusion pipeline loads its upscaler and extractor in the services the upscale and extract views use.
    /// </summary>
    public enum ModelOwnerType
    {
        None = 0,
        Diffusion = 1,
        Language = 2,
        Upscale = 3,
        Extract = 4,
        Interpolation = 5,
        Audio = 6
    }
}
