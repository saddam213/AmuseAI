// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for AppDisclaimerDialog.xaml
    /// </summary>
    public partial class AppDisclaimerDialog : DialogControl
    {
        private bool _isAccepted;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppDisclaimerDialog"/> class.
        /// </summary>
        public AppDisclaimerDialog()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanExecuteCancel);
            InitializeComponent();
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public bool IsAccepted
        {
            get { return _isAccepted; }
            set { SetProperty(ref _isAccepted, value); }
        }


        /// <summary>
        /// Determines whether the disclaimer can be continued, it must be accepted first.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            return _isAccepted;
        }
    }
}
