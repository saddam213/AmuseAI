using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum MemoryMode
    {
        [Display(Name = "MemoryMode0", Description = "MemoryMode0_Description", ResourceType = typeof(AppEnums))]
        Auto = 0,

        [Display(Name = "MemoryMode1", Description = "MemoryMode1_Description", ResourceType = typeof(AppEnums))]
        Balanced = 1,

        [Display(Name = "MemoryMode2", Description = "MemoryMode2_Description", ResourceType = typeof(AppEnums))]
        Low = 2,

        [Display(Name = "MemoryMode3", Description = "MemoryMode3_Description", ResourceType = typeof(AppEnums))]
        Medium = 3,

        [Display(Name = "MemoryMode4", Description = "MemoryMode4_Description", ResourceType = typeof(AppEnums))]
        High = 4
    }
}
