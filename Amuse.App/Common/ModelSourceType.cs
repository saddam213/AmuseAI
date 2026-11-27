using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum ModelSourceType
    {
        [Display(Name = "ModelSourceType0", Description = "ModelSourceType0_Description", ResourceType = typeof(AppEnums))]
        LocalFile = 0,

        [Display(Name = "ModelSourceType1", Description = "ModelSourceType1_Description", ResourceType = typeof(AppEnums))]
        LocalFolder = 1,

        [Display(Name = "ModelSourceType2", Description = "ModelSourceType2_Description", ResourceType = typeof(AppEnums))]
        LocalCheckpoint = 2,

        [Display(Name = "ModelSourceType3", Description = "ModelSourceType3_Description", ResourceType = typeof(AppEnums))]
        Checkpoint = 3,

        [Display(Name = "ModelSourceType4", Description = "ModelSourceType4_Description", ResourceType = typeof(AppEnums))]
        LocalFileVision = 4
    }
}
