using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum MemoryMode
    {
        [Display(Description = "Automatically selects the best strategy.")]
        Auto = 0,

        [Display(Description = "Loads model weights across GPUs and CPU.")]
        Balanced = 1,

        [Display(Description = "Loads tensor weights to GPU only when needed.")]
        Low = 2,

        [Display(Description = "Loads model weights to GPU only when needed.")]
        Medium = 3,

        [Display(Description = "Loads all weights to GPU for peak performance.")]
        High = 4
    }


    public enum QualityMode
    {
        [Display(Description = "Uses 4-bit quantization (int4). Maximum memory savings with a noticeable loss in fine detail and texture clarity.")]
        Draft = 0,

        [Display(Description = "Uses 8-bit quantization (float8). High visual fidelity with significant memory optimization; indistinguishable from high for most users.")]
        Standard = 1,

        [Display(Description = "Uses 16-bit precision (bfloat16). Original model weights with zero quality loss; requires the most VRAM for maximum accuracy.")]
        Production = 2,
    }
}
