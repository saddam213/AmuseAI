// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.Common;
using System;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ExecutionOptionsDialog.xaml
    /// </summary>
    public partial class ExecutionOptionsDialog : DialogControl
    {
        private BackendType _backend;
        private ExecutionOptionsModel _executionOptions;
        private AutoOption _selectedConvDirect;
        private AutoOption _selectedFlashAttention;
        private DataType[] _weightTypes;

        public ExecutionOptionsDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            ResetCommand = new AsyncRelayCommand(ResetAsync);
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand ResetCommand { get; }

        public BackendType Backend
        {
            get { return _backend; }
            set { SetProperty(ref _backend, value); }
        }

        public ExecutionOptionsModel ExecutionOptions
        {
            get { return _executionOptions; }
            set { SetProperty(ref _executionOptions, value); }
        }

        public DataType[] WeightTypes
        {
            get { return _weightTypes; }
            set { SetProperty(ref _weightTypes, value); }
        }

        public AutoOption SelectedConvDirect
        {
            get { return _selectedConvDirect; }
            set
            {
                SetProperty(ref _selectedConvDirect, value);
                if (ExecutionOptions != null)
                {
                    ExecutionOptions.DiffusionConvDirect = _selectedConvDirect;
                    ExecutionOptions.AutoEncoderConvDirect = _selectedConvDirect;
                    ExecutionOptions.TextEncoderConvDirect = _selectedConvDirect;
                    ExecutionOptions.ControlNetConvDirect = _selectedConvDirect;
                }
            }
        }

        public AutoOption SelectedFlashAttention
        {
            get { return _selectedFlashAttention; }
            set
            {
                SetProperty(ref _selectedFlashAttention, value);
                if (ExecutionOptions != null)
                {
                    ExecutionOptions.DiffusionFlashAttention = _selectedFlashAttention;
                    ExecutionOptions.AutoEncoderFlashAttention = _selectedFlashAttention;
                    ExecutionOptions.TextEncoderFlashAttention = _selectedFlashAttention;
                    ExecutionOptions.ControlNetFlashAttention = _selectedFlashAttention;
                }
            }
        }


        public Task<bool> ShowDialogAsync(BackendType backend, ExecutionOptionsModel executionOptions)
        {
            Backend = backend;
            WeightTypes = backend.GetWeightTypes();
            ExecutionOptions = executionOptions.DeepClone();
            SetSelectedValues();
            return base.ShowDialogAsync();
        }

        /// <summary>
        /// Resets the options to their defaults, they are saved when the dialog is saved.
        /// </summary>
        private Task ResetAsync()
        {
            ExecutionOptions = new ExecutionOptionsModel();
            SetSelectedValues();
            return Task.CompletedTask;
        }


        private void SetSelectedValues()
        {
            foreach (var value in Enum.GetValues<AutoOption>())
            {
                if (ExecutionOptions.DiffusionConvDirect == value
                 && ExecutionOptions.AutoEncoderConvDirect == value
                 && ExecutionOptions.TextEncoderConvDirect == value
                 && ExecutionOptions.ControlNetConvDirect == value)
                {
                    SelectedConvDirect = value;
                }

                if (ExecutionOptions.DiffusionFlashAttention == value
                 && ExecutionOptions.AutoEncoderFlashAttention == value
                 && ExecutionOptions.TextEncoderFlashAttention == value
                 && ExecutionOptions.ControlNetFlashAttention == value)
                {
                    SelectedFlashAttention = value;
                }
            }
        }
    }
}
