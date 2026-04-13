// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using System.Collections.Generic;

namespace Amuse.App.Common
{
    public class TemplateSettings
    {
        public List<DiffusionModel> DiffusionTemplates { get; set; }
        public List<WizardItemModel> DiffusionTemplateMap { get; set; }
    }
}
