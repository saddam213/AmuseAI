using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum ExtractorType
    {
        [Display(Name = "ExtractorType0", ResourceType = typeof(AppEnums))]
        Default = 0,

        [Display(Name = "ExtractorType1", ResourceType = typeof(AppEnums))]
        Background = 1,

        [Display(Name = "ExtractorType2", ResourceType = typeof(AppEnums))]
        Pose = 2
    }
}
