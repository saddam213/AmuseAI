using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum CheckpointType
    {
        [Display(Name = "CheckpointType0", ResourceType = typeof(AppEnums))]
        LocalFolder = 0,

        [Display(Name = "CheckpointType1", ResourceType = typeof(AppEnums))]
        LocalFile = 1,

        [Display(Name = "CheckpointType2", ResourceType = typeof(AppEnums))]
        OnlineFolder = 20,

        [Display(Name = "CheckpointType3", ResourceType = typeof(AppEnums))]
        OnlineFile = 21,

        [Display(Name = "CheckpointType4", ResourceType = typeof(AppEnums))]
        Component = 100
    }
}
