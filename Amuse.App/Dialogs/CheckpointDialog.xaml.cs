// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for CheckpointDialog.xaml
    /// </summary>
    public partial class CheckpointDialog : DialogControl
    {
        private string _message;
        private IReadOnlyList<ICheckpointModel> _checkpoints;
        private ICheckpointModel _selectedCheckpoint;
        private bool _isSelectorVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckpointDialog"/> class.
        /// </summary>
        public CheckpointDialog()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync, CanExecuteCancel);
            InitializeComponent();
        }

        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        public IReadOnlyList<ICheckpointModel> Checkpoints
        {
            get { return _checkpoints; }
            set { SetProperty(ref _checkpoints, value); }
        }

        public ICheckpointModel SelectedCheckpoint
        {
            get { return _selectedCheckpoint; }
            set
            {
                SetProperty(ref _selectedCheckpoint, value);
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsSelectorVisible
        {
            get { return _isSelectorVisible; }
            set { SetProperty(ref _isSelectorVisible, value); }
        }


        /// <summary>
        /// Shows the dialog to queue the model for download, with a checkpoint set selector when the user can choose one.
        /// </summary>
        /// <param name="model">The model, or <c>null</c> to only ask the question for a pipeline without a generate model.</param>
        /// <param name="checkpointType">The checkpoint set type to select, the current checkpoint set when null.</param>
        /// <param name="isSelectorVisible">if set to <c>true</c> the checkpoint set selector is shown.</param>
        public Task<bool> ShowDialogAsync(IGenerateModel model, DataType? checkpointType, bool isSelectorVisible)
        {
            IsSelectorVisible = isSelectorVisible && model is not null;
            Message = _isSelectorVisible
                ? string.Format(AppDefault.QuantDownloadMessage, model.Name)
                : AppDefault.PipelineQueueDownloadMessage;
            Checkpoints = model?.GetCheckpoints() ?? [];
            SelectedCheckpoint = _checkpoints.FirstOrDefault(x => x.Type == checkpointType)
                              ?? _checkpoints.FirstOrDefault(x => x.Type == model?.CurrentCheckpointType)
                              ?? _checkpoints.FirstOrDefault();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Determines whether a checkpoint set is selected, or no selection is needed.
        /// </summary>
        private bool CanSave()
        {
            return !_isSelectorVisible || _selectedCheckpoint is not null;
        }


        protected override void OnLoaded()
        {
            base.OnLoaded();
            Dispatcher.BeginInvoke(() =>
            {
                CheckpointListBox.Focus();
            }, DispatcherPriority.Input);
        }
    }
}
