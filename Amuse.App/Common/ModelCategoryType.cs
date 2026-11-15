// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Resources;
using System.ComponentModel.DataAnnotations;

namespace Amuse.App.Common
{
    public enum ModelCategoryType
    {
        [Display(Name = "ModelCategoryType0", ResourceType = typeof(AppEnums))]
        Diffusion = 0,

        [Display(Name = "ModelCategoryType1", ResourceType = typeof(AppEnums))]
        ControlNet = 1,

        [Display(Name = "ModelCategoryType2", ResourceType = typeof(AppEnums))]
        LoraAdapter = 2,

        [Display(Name = "ModelCategoryType3", ResourceType = typeof(AppEnums))]
        Upscale = 10,

        [Display(Name = "ModelCategoryType4", ResourceType = typeof(AppEnums))]
        Extract = 20,

        [Display(Name = "ModelCategoryType5", ResourceType = typeof(AppEnums))]
        Component = 30,

        [Display(Name = "ModelCategoryType6", ResourceType = typeof(AppEnums))]
        LLM = 40
    }
}
