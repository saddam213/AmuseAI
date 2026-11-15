using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum ModelStatusType
    {
        [Display(Name = "ModelStatusType0", ResourceType = typeof(AppEnums))]
        Available = 0,

        [Display(Name = "ModelStatusType1", ResourceType = typeof(AppEnums))]
        Unknown = 1,

        [Display(Name = "ModelStatusType2", ResourceType = typeof(AppEnums))]
        Downloading = 11,

        [Display(Name = "ModelStatusType3", ResourceType = typeof(AppEnums))]
        DownloadQueue = 12,

        [Display(Name = "ModelStatusType4", ResourceType = typeof(AppEnums))]
        DownloadFailed = 13,

        [Display(Name = "ModelStatusType5", ResourceType = typeof(AppEnums))]
        Installed = 20
    }
}
