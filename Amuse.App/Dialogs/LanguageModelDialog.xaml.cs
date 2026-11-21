// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Views;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for LanguageModelDialog.xaml
    /// </summary>
    public partial class LanguageModelDialog : DialogControl
    {
        private LanguageModel _languageModel;
        private LanguageModel _originalLanguageModel;
        private LanguageCheckpointModel _checkpointModel;
        private SchedulerInputOptions[] _schedulers;
        private DataType[] _dataTypes;

        public LanguageModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            EstimateMemoryCommand = new AsyncRelayCommand(EstimateMemoryAsync);
            Errors = new ObservableCollection<string>();
            AccessTokens = [new AccessToken("None", null), .. settings.AccessTokens.Select(x => new AccessToken(x.Name, x.Name))];
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand EstimateMemoryCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public bool IsUpdateMode => _originalLanguageModel is not null;
        /// <summary>
        /// Gets or sets the data types the model backend can load.
        /// </summary>
        public DataType[] DataTypes
        {
            get { return _dataTypes; }
            set { SetProperty(ref _dataTypes, value); }
        }
        public AccessToken[] AccessTokens { get; }

        public LanguageModel LanguageModel
        {
            get { return _languageModel; }
            set { SetProperty(ref _languageModel, value); }
        }

        public LanguageCheckpointModel CheckpointModel
        {
            get { return _checkpointModel; }
            set { SetProperty(ref _checkpointModel, value); }
        }

        public SchedulerInputOptions[] Schedulers
        {
            get { return _schedulers; }
            set { SetProperty(ref _schedulers, value); }
        }


        public Task<bool> UpdateAsync(LanguageModel languageModel)
        {
            var modelId = languageModel.Id;
            _originalLanguageModel = languageModel;
            LanguageModel = languageModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(LanguageModel languageModel)
        {
            var modelId = Settings.LanguageModels.NextId();
            LanguageModel = languageModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(LanguageModel[] modelImports)
        {
            var modelId = Settings.LanguageModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                LanguageModel = modelImport;
                Populate();
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.LanguageModels.Any(x => x.Backend == modelImport.Backend && x.Name == modelImport.Name))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.LanguageModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Models Imported.");
                return true;
            }
        }


        protected override Task SaveAsync()
        {
            LanguageModel.ProcessTypes = GetProcessTypes();
            LanguageModel.ViewFilter = GetViewFilter();
            LanguageModel.Initialize(Settings);

            var index = Settings.LanguageModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.LanguageModels.IndexOf(_originalLanguageModel);
                Settings.LanguageModels.Remove(_originalLanguageModel);
            }
            Settings.LanguageModels.Insert(index, LanguageModel);
            LanguageModel.Initialize(Settings);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (LanguageModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            LanguageModel = default;
            _originalLanguageModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private void Populate()
        {
            // DataTypes, keep the current type if the backend list does not have it
            var dataTypes = LanguageModel.Backend.GetDataTypes();
            DataTypes = dataTypes.Contains(LanguageModel.BaseType) ? dataTypes : [.. dataTypes, LanguageModel.BaseType];

            if (LanguageModel.DefaultOptions.Schedulers != null)
                Schedulers = LanguageModel.DefaultOptions.Schedulers.Copy();

            SetViewFilters();
            SetProcessTypes();
            CheckpointModel = LanguageModel.Checkpoint;
            NotifyPropertyChanged(nameof(AccessTokens));
            NotifyPropertyChanged(nameof(IsUpdateMode));
            LanguageModel.NotifyPropertyChanged(nameof(LanguageModel.AccessToken));
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(LanguageModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.LanguageModels.Any(x => x.Name.Equals(LanguageModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{LanguageModel.Name}' already exists";
            }

            if (LanguageModel.DefaultOptions.Steps2 < 0)
                yield return "Steps2 must be be >= 0";
            if (LanguageModel.DefaultOptions.GuidanceScale < 0)
                yield return "GuidanceScale must be be >= 0";
            if (LanguageModel.DefaultOptions.GuidanceScale2 < 0)
                yield return "GuidanceScale2 must be be >= 0";
            if (LanguageModel.DefaultOptions.Strength < 0)
                yield return "Strength must be be >= 0";
            if (LanguageModel.DefaultOptions.Channels < 0)
                yield return "Channels must be be >= 0";
            if (LanguageModel.DefaultOptions.MinLength < 0)
                yield return "MinLength must be be >= 0";
            if (LanguageModel.DefaultOptions.MaxLength < 0)
                yield return "MaxLength must be be >= 0";
            if (LanguageModel.DefaultOptions.MaxLength2 < 0)
                yield return "MaxLength2 must be be >= 0";
            if (LanguageModel.DefaultOptions.Duration < 0)
                yield return "Duration must be be >= 0";
            if (LanguageModel.DefaultOptions.SilenceDuration < 0)
                yield return "SilenceDuration must be be >= 0";
            if (LanguageModel.DefaultOptions.FrameChunkOverlap < 0)
                yield return "FrameChunkOverlap must be be >= 0";
            if (LanguageModel.DefaultOptions.NoiseCondition < 0)
                yield return "NoiseCondition must be be >= 0";
            if (LanguageModel.DefaultOptions.FrameChunk < 0)
                yield return "FrameChunk must be be >= 0";
            if (LanguageModel.DefaultOptions.NoRepeatNgramSize < 0)
                yield return "NoRepeatNgramSize must be be >= 0";
            if (LanguageModel.DefaultOptions.Beams < 0)
                yield return "Beams must be be >= 0";
            if (LanguageModel.DefaultOptions.TopP < 0)
                yield return "TopP must be be >= 0";
            if (LanguageModel.DefaultOptions.ChunkSize < 0)
                yield return "ChunkSize must be be >= 0";

            // Memory
            if (LanguageModel.MemorySize < 0)
                yield return "Memory Size must be >= 0";

            // ProcessTypes
            var processTypes = GetProcessTypes();
            if (processTypes.IsNullOrEmpty())
                yield return "ProcessTypes cannot be empty";

            // Checkpoint
            foreach (var checkpoint in LanguageModel.Checkpoint.GetComponents())
            {
                if (!checkpoint.IsValid(out var checkpointValidation))
                    yield return $"{checkpoint.Name} {checkpointValidation}";
            }
        }


        private void SetProcessTypes()
        {
            foreach (var processType in LanguageModel.ProcessTypes)
            {
                if (processType == ProcessType.TextToText)
                    CheckBoxTextToText.IsChecked = true;
                if (processType == ProcessType.ImageToText)
                    CheckBoxImageToText.IsChecked = true;
            }
        }


        private ProcessType[] GetProcessTypes()
        {
            IEnumerable<ProcessType> ProcessTypes()
            {
                if (CheckBoxTextToText.IsChecked == true)
                    yield return ProcessType.TextToText;
                if (CheckBoxImageToText.IsChecked == true)
                    yield return ProcessType.ImageToText;
            }
            return [.. ProcessTypes()];
        }


        private View[] GetViewFilter()
        {
            IEnumerable<View> ViewFilters()
            {
                if (CheckBoxViewImageToText.IsChecked == true)
                    yield return View.ImageToText;
                if (CheckBoxViewTextInstruct.IsChecked == true)
                    yield return View.TextInstruct;
                if (CheckBoxViewTextConverse.IsChecked == true)
                    yield return View.TextConverse;
            }

            var viewFilters = ViewFilters().ToArray();
            if (viewFilters.IsNullOrEmpty())
                return null;

            return viewFilters;
        }


        private void SetViewFilters()
        {
            if (LanguageModel.ViewFilter.IsNullOrEmpty())
                return;

            foreach (var viewType in LanguageModel.ViewFilter)
            {
                if (viewType == View.ImageToText)
                    CheckBoxViewImageToText.IsChecked = true;
                if (viewType == View.TextInstruct)
                    CheckBoxViewTextInstruct.IsChecked = true;
                if (viewType == View.TextConverse)
                    CheckBoxViewTextConverse.IsChecked = true;
            }
        }


        private Task EstimateMemoryAsync()
        {
            if (_languageModel.ModelParams > 0)
            {
                _languageModel.MemorySize = Utils.GetMemorySize(_languageModel.ModelParams);
            }
            return Task.CompletedTask;
        }


        public record AccessToken(string Name, string Value);
    }
}
