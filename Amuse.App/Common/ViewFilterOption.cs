// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Views;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    /// <summary>
    /// A view the user can limit a model to, shown as a check box.
    /// </summary>
    public sealed class ViewFilterOption : BaseModel
    {
        private bool _isSelected;

        public View View { get; init; }
        public string Name { get; init; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
    }
}
