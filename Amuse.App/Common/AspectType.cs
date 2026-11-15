using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;
namespace Amuse.App.Common
{
    public enum AspectType
    {
        [Display(Name = "AspectType0", ResourceType = typeof(AppEnums))]
        Square = 0,

        [Display(Name = "AspectType1", ResourceType = typeof(AppEnums))]
        Portrait = 1,

        [Display(Name = "AspectType2", ResourceType = typeof(AppEnums))]
        Landscape = 2
    }
}
