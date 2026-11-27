using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    /// <summary>
    /// The fixed folders of the model directory, shown by their folder name on disk.
    /// </summary>
    public enum ModelFolderType
    {
        [Display(Name = ModelFolders.Diffusion)]
        Diffusion = 0,

        [Display(Name = ModelFolders.TextEncoder)]
        TextEncoder = 1,

        [Display(Name = ModelFolders.LLM)]
        LLM = 2,

        [Display(Name = ModelFolders.AutoEncoder)]
        AutoEncoder = 3,

        [Display(Name = ModelFolders.LoraAdapter)]
        LoraAdapter = 4,

        [Display(Name = ModelFolders.ControlNet)]
        ControlNet = 5,

        [Display(Name = ModelFolders.Upscale)]
        Upscale = 6,

        [Display(Name = ModelFolders.Extract)]
        Extract = 7,

        [Display(Name = ModelFolders.Tokenizer)]
        Tokenizer = 8,

        [Display(Name = ModelFolders.Components)]
        Components = 9
    }
}
