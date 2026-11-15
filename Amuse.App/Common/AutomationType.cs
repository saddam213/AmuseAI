using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum AutomationType
    {
        [Display(Name = "AutomationType0", Description = "AutomationType0_Description", ResourceType = typeof(AppEnums))]
        Seed = 0,

        [Display(Name = "AutomationType1", Description = "AutomationType1_Description", ResourceType = typeof(AppEnums))]
        PromptLines = 10,

        [Display(Name = "AutomationType2", Description = "AutomationType2_Description", ResourceType = typeof(AppEnums))]
        PromptFiles = 11,

        [Display(Name = "AutomationType3", Description = "AutomationType3_Description", ResourceType = typeof(AppEnums))]
        InputFiles = 12
    }
}
