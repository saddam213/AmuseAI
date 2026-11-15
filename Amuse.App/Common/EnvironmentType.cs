using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum EnvironmentType
    {
        [Display(Name = "EnvironmentType0", ResourceType = typeof(AppEnums))]
        Vendor = 0,

        [Display(Name = "EnvironmentType1", ResourceType = typeof(AppEnums))]
        Device = 1,

        [Display(Name = "EnvironmentType2", ResourceType = typeof(AppEnums))]
        Pipeline = 2
    }
}
