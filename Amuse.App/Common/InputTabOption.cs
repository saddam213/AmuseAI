using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;
namespace Amuse.App.Common
{
    public enum InputTabOption
    {
        [Display(Name = "InputTabOption0", ResourceType = typeof(AppEnums))]
        Options = 0,

        [Display(Name = "InputTabOption1", ResourceType = typeof(AppEnums))]
        Advanced = 1,

        [Display(Name = "InputTabOption2", ResourceType = typeof(AppEnums))]
        Automation = 2
    }
}
