// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
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
using TensorStack.WPF.Resources;
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
        private DataType[] _checkpointTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public LanguageModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddCheckpointCommand = new AsyncRelayCommand(AddCheckpointAsync, CanAddCheckpoint);
            RemoveCheckpointCommand = new AsyncRelayCommand(RemoveCheckpointAsync, CanRemoveCheckpoint);
            EstimateMemoryCommand = new AsyncRelayCommand(EstimateMemoryAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand AddCheckpointCommand { get; }
        public AsyncRelayCommand RemoveCheckpointCommand { get; }
        public AsyncRelayCommand EstimateMemoryCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public bool IsUpdateMode => _originalLanguageModel is not null;
        /// <summary>
        /// Gets or sets the data types a checkpoint set can be labelled with.
        /// </summary>
        public DataType[] CheckpointTypes
        {
            get { return _checkpointTypes; }
            set { SetProperty(ref _checkpointTypes, value); }
        }

        public LanguageModel LanguageModel
        {
            get { return _languageModel; }
            set { SetProperty(ref _languageModel, value); }
        }

        public LanguageCheckpointModel CheckpointModel
        {
            get { return _checkpointModel; }
            set
            {
                SetProperty(ref _checkpointModel, value);
                AddCheckpointCommand.RaiseCanExecuteChanged();
                RemoveCheckpointCommand.RaiseCanExecuteChanged();
            }
        }


        /// <summary>
        /// Shows the dialog to update a model.
        /// </summary>
        /// <param name="languageModel">The model to update.</param>
        public Task<bool> UpdateAsync(LanguageModel languageModel)
        {
            var modelId = languageModel.Id;
            _originalLanguageModel = languageModel;
            LanguageModel = languageModel.DeepClone(modelId);
            LanguageModel.IsDefault = languageModel.IsDefault;
            LanguageModel.IsNew = languageModel.IsNew;
            LanguageModel.IsRecommended = languageModel.IsRecommended;
            LanguageModel.UserMemoryMode = languageModel.UserMemoryMode;
            LanguageModel.UserExecutionOptions = languageModel.UserExecutionOptions;
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of a model.
        /// </summary>
        /// <param name="languageModel">The model to copy.</param>
        public Task<bool> CopyAsync(LanguageModel languageModel)
        {
            var modelId = Settings.LanguageModels.NextId();
            LanguageModel = languageModel.DeepClone(modelId);
            LanguageModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports models, showing the dialog for a single model and adding several directly.
        /// </summary>
        /// <param name="modelImports">The models to import.</param>
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

                    if (!modelImport.GetComponents().Any())
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.LanguageModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteModels, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the model, replacing the original when updating.
        /// </summary>
        protected override Task SaveAsync()
        {
            LanguageModel.Name = LanguageModel.Name.Trim();
            LanguageModel.ProcessTypes = GetProcessTypes();
            LanguageModel.ViewFilter = GetViewFilter();
            if (LanguageModel.Checkpoints.All(x => x.Type != LanguageModel.CurrentCheckpointType))
                LanguageModel.CurrentCheckpointType = null;

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


        /// <summary>
        /// Determines whether the model can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (LanguageModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        /// <summary>
        /// Cancels the dialog.
        /// </summary>
        protected override Task CancelAsync()
        {
            LanguageModel = default;
            _originalLanguageModel = null;
            return base.CancelAsync();
        }


        /// <summary>
        /// Closes the dialog.
        /// </summary>
        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        /// <summary>
        /// Fills the functions, view filters and quality sets from the model.
        /// </summary>
        private void Populate()
        {
            var dataTypes = LanguageModel.Backend.GetDataTypes();

            SetViewFilters();
            SetProcessTypes();
            CheckpointTypes = [.. new[] { DataType.Default }.Concat(dataTypes).Concat(LanguageModel.Checkpoints.Select(x => x.Type)).Distinct()];
            CheckpointModel = LanguageModel.Checkpoint;
            NotifyPropertyChanged(nameof(IsUpdateMode));
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(LanguageModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.LanguageModels.Any(x => x != _originalLanguageModel && x.Name.Equals(LanguageModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, LanguageModel.Name.Trim());

            if (LanguageModel.ModelParams < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.ModelParametersB);
            if (LanguageModel.ModelSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.ModelSizeGB);
            if (LanguageModel.DefaultOptions.MinLength < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MinLength);
            if (LanguageModel.DefaultOptions.MaxLength < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MaxLength);
            if (LanguageModel.DefaultOptions.MaxLength > 0 && LanguageModel.DefaultOptions.MinLength > LanguageModel.DefaultOptions.MaxLength)
                yield return string.Format(AppErrors.ValidationLessOrEqual, AppDefault.MinLength, AppDefault.MaxLength);
            if (LanguageModel.DefaultOptions.ChunkSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.ChunkSize);
            if (LanguageModel.DefaultOptions.Beams < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Beams);
            if (LanguageModel.DefaultOptions.TopK < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.TopK);
            if (LanguageModel.DefaultOptions.TopP < 0 || LanguageModel.DefaultOptions.TopP > 1)
                yield return string.Format(AppErrors.ValidationBetween, AppDefault.TopP, 0, 1);
            if (LanguageModel.DefaultOptions.TypicalP < 0 || LanguageModel.DefaultOptions.TypicalP > 1)
                yield return string.Format(AppErrors.ValidationBetween, AppDefault.TypicalP, 0, 1);
            if (LanguageModel.DefaultOptions.Temperature < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Temperature);
            if (LanguageModel.DefaultOptions.RepetitionPenalty < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.RepetitionPenalty);
            if (LanguageModel.DefaultOptions.NoRepeatNgramSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.NoRepeatNgrams);

            var processTypes = GetProcessTypes();
            if (processTypes.IsNullOrEmpty())
                yield return AppErrors.ValidationProcessTypesEmpty;

            if (LanguageModel.Checkpoints.Count == 0 || LanguageModel.Checkpoints.Any(x => !x.GetComponents().Any()))
                yield return AppErrors.ValidationCheckpointEmpty;

            foreach (var checkpointSet in LanguageModel.Checkpoints)
            {
                if (checkpointSet.MemorySize < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MemorySize);

                foreach (var checkpoint in checkpointSet.GetComponents())
                {
                    if (!checkpoint.IsValid(out var checkpointValidation))
                        yield return LanguageModel.HasCheckpointSets
                            ? $"{checkpointSet.Type.GetDisplayName()} {checkpoint.Name} {checkpointValidation}"
                            : $"{checkpoint.Name} {checkpointValidation}";
                }
            }
            if (LanguageModel.Checkpoints.Select(x => x.Type).Distinct().Count() != LanguageModel.Checkpoints.Count)
                yield return AppErrors.ValidationQuantTypeExists;
        }


        /// <summary>
        /// Checks the supported functions of the model.
        /// </summary>
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


        /// <summary>
        /// Gets the checked supported functions.
        /// </summary>
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


        /// <summary>
        /// Gets the checked views, <c>null</c> when none are checked.
        /// </summary>
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


        /// <summary>
        /// Checks the views of the model view filter.
        /// </summary>
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


        /// <summary>
        /// Estimates the memory size of the selected quality set from the model parameters.
        /// </summary>
        private Task EstimateMemoryAsync()
        {
            if (_languageModel.ModelParams > 0 && _checkpointModel is not null)
            {
                _checkpointModel.MemorySize = Utils.GetMemorySize(_languageModel.ModelParams, _checkpointModel.Type);
            }
            return Task.CompletedTask;
        }


        /// <summary>
        /// Adds a checkpoint set copied from the selected one, with the first type not already used.
        /// </summary>
        private Task AddCheckpointAsync()
        {
            var checkpoint = _checkpointModel.DeepClone();
            checkpoint.Type = _checkpointTypes.FirstOrDefault(x => LanguageModel.Checkpoints.All(c => c.Type != x));
            LanguageModel.Checkpoints = [.. LanguageModel.Checkpoints, checkpoint];
            CheckpointModel = checkpoint;
            RemoveCheckpointCommand.RaiseCanExecuteChanged();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether there is a selected checkpoint set to copy.
        /// </summary>
        private bool CanAddCheckpoint()
        {
            return _checkpointModel is not null && _checkpointTypes?.Any(x => LanguageModel.Checkpoints.All(c => c.Type != x)) == true;
        }


        /// <summary>
        /// Removes the selected checkpoint set and selects the first one left.
        /// </summary>
        private Task RemoveCheckpointAsync()
        {
            LanguageModel.Checkpoints = [.. LanguageModel.Checkpoints.Where(x => x != _checkpointModel)];
            CheckpointModel = LanguageModel.Checkpoints.FirstOrDefault();
            RemoveCheckpointCommand.RaiseCanExecuteChanged();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the selected checkpoint set can be removed, a model keeps at least one.
        /// </summary>
        private bool CanRemoveCheckpoint()
        {
            return _checkpointModel is not null && LanguageModel?.Checkpoints.Count > 1;
        }
    }
}
