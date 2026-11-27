// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        private AutoOption? _selectedConvDirect;
        private AutoOption? _selectedFlashAttention;
        private DataType[] _weightTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionOptionsDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ExecutionOptionsDialog(Settings settings)
        {
            Settings = settings;
            Errors = new ObservableCollection<string>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            ResetCommand = new AsyncRelayCommand(ResetAsync);
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand ResetCommand { get; }
        public ObservableCollection<string> Errors { get; }

        public BackendType Backend
        {
            get { return _backend; }
            set { SetProperty(ref _backend, value); }
        }

        public ExecutionOptionsModel ExecutionOptions
        {
            get { return _executionOptions; }
            set { SetExecutionOptions(value); }
        }

        public DataType[] WeightTypes
        {
            get { return _weightTypes; }
            set { SetProperty(ref _weightTypes, value); }
        }

        public AutoOption? SelectedConvDirect
        {
            get { return _selectedConvDirect; }
            set
            {
                SetProperty(ref _selectedConvDirect, value);
                if (_executionOptions != null && value.HasValue)
                {
                    _executionOptions.DiffusionConvDirect = value.Value;
                    _executionOptions.AutoEncoderConvDirect = value.Value;
                    _executionOptions.TextEncoderConvDirect = value.Value;
                    _executionOptions.ControlNetConvDirect = value.Value;
                }
            }
        }

        public AutoOption? SelectedFlashAttention
        {
            get { return _selectedFlashAttention; }
            set
            {
                SetProperty(ref _selectedFlashAttention, value);
                if (_executionOptions != null && value.HasValue)
                {
                    _executionOptions.DiffusionFlashAttention = value.Value;
                    _executionOptions.AutoEncoderFlashAttention = value.Value;
                    _executionOptions.TextEncoderFlashAttention = value.Value;
                    _executionOptions.ControlNetFlashAttention = value.Value;
                }
            }
        }


        /// <summary>
        /// Shows the dialog with a copy of the execution options of a backend.
        /// </summary>
        /// <param name="backend">The backend.</param>
        /// <param name="executionOptions">The execution options.</param>
        public Task<bool> ShowDialogAsync(BackendType backend, ExecutionOptionsModel executionOptions)
        {
            Backend = backend;
            WeightTypes = backend.GetWeightTypes();
            ExecutionOptions = executionOptions.DeepClone();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Determines whether the options can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Saves the options, releasing the options subscription and keeping the options for the caller.
        /// </summary>
        protected override Task SaveAsync()
        {
            ReleaseExecutionOptions();
            return base.SaveAsync();
        }


        /// <summary>
        /// Cancels the dialog, releasing the options subscription.
        /// </summary>
        protected override Task CancelAsync()
        {
            ReleaseExecutionOptions();
            return base.CancelAsync();
        }


        /// <summary>
        /// Closes the dialog, releasing the options subscription.
        /// </summary>
        protected override Task CloseAsync()
        {
            ReleaseExecutionOptions();
            return base.CloseAsync();
        }


        /// <summary>
        /// Stops watching the options, so the options handed back to the caller no longer reference the dialog.
        /// </summary>
        private void ReleaseExecutionOptions()
        {
            if (_executionOptions != null)
                _executionOptions.PropertyChanged -= OnExecutionOptionsChanged;
        }


        /// <summary>
        /// Resets the options to their defaults, they are saved when the dialog is saved.
        /// </summary>
        private Task ResetAsync()
        {
            ExecutionOptions = new ExecutionOptionsModel();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Sets the options and watches them, so the All choices follow the parts.
        /// </summary>
        /// <param name="executionOptions">The options, none when <c>null</c>.</param>
        private void SetExecutionOptions(ExecutionOptionsModel executionOptions)
        {
            if (_executionOptions != null)
                _executionOptions.PropertyChanged -= OnExecutionOptionsChanged;

            SetProperty(ref _executionOptions, executionOptions, nameof(ExecutionOptions));
            if (_executionOptions != null)
            {
                _executionOptions.PropertyChanged += OnExecutionOptionsChanged;
                UpdateAllChoices();
            }
        }


        /// <summary>
        /// Called when an option changes.
        /// </summary>
        /// <param name="sender">The options.</param>
        /// <param name="e">The event args.</param>
        private void OnExecutionOptionsChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateAllChoices();
        }


        /// <summary>
        /// Shows the value every part shares in the All choices, nothing when the parts differ.
        /// </summary>
        private void UpdateAllChoices()
        {
            var options = _executionOptions;
            _selectedConvDirect = GetSharedValue(options.DiffusionConvDirect, options.AutoEncoderConvDirect, options.TextEncoderConvDirect, options.ControlNetConvDirect);
            _selectedFlashAttention = GetSharedValue(options.DiffusionFlashAttention, options.AutoEncoderFlashAttention, options.TextEncoderFlashAttention, options.ControlNetFlashAttention);
            NotifyPropertyChanged(nameof(SelectedConvDirect));
            NotifyPropertyChanged(nameof(SelectedFlashAttention));
        }


        /// <summary>
        /// Gets the value all parts share, <c>null</c> when they differ.
        /// </summary>
        /// <param name="values">The part values.</param>
        private static AutoOption? GetSharedValue(params AutoOption[] values)
        {
            return values.Distinct().Count() == 1 ? values[0] : null;
        }


        /// <summary>
        /// Gets the validation errors of the options the backend shows.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (_executionOptions is null)
                yield break;

            if (_backend == BackendType.LlamaCpp)
            {
                if (_executionOptions.Threads < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Threads);
                if (_executionOptions.GpuLayers < -1)
                    yield return string.Format(AppErrors.ValidationMinimum, AppDefault.GpuLayers, -1);
                if (_executionOptions.ContextSize < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.ContextSize);
            }
            else if (_backend == BackendType.StableDiffusionCpp)
            {
                if (_executionOptions.MemoryReserve < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MemoryReserveGB);
            }
        }
    }
}
