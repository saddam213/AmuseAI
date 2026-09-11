// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Views;
using Amuse.Common;
using TensorStack.WPF.Controls;

namespace Amuse.App.Common
{
    public record ModelViewOpenArgs(View View, ModelCategoryType ModelType, PipelineType? PipelineType = null) : OpenViewArgs;
}
