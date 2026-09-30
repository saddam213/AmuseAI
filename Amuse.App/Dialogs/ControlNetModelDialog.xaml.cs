// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for ControlNetModelDialog.xaml
    /// </summary>
    public partial class ControlNetModelDialog : DialogControl
    {
        private ControlNetModel _controlNetModel;
        private ControlNetModel _originalControlNetModel;

        public ControlNetModelDialog(Settings settings)
        {
            Settings = settings;
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFolder, CheckpointType.LocalFolder];
        public bool IsUpdateMode => _originalControlNetModel is not null;

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            set { SetProperty(ref _controlNetModel, value); }
        }


        public async Task<bool> AddAsync()
        {
            return await AddAsync(Settings.DiffusionPipelines.First());
        }


        public Task<bool> AddAsync(PipelineType pipelineType)
        {
            var modelId = Settings.ControlNetModels.NextId();
            ControlNetModel = new ControlNetModel
            {
                Id = modelId,
                Pipeline = pipelineType,
                Name = "New ControlNet",
                Status = ModelStatusType.Unknown,
                Checkpoint = new CheckpointComponent
                {
                    Name = "ControlNet",
                    Type = CheckpointType.LocalFolder
                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(ControlNetModel controlNetModel)
        {
            var modelId = controlNetModel.Id;
            _originalControlNetModel = controlNetModel;
            ControlNetModel = controlNetModel.DeepClone(modelId);
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(ControlNetModel controlNetModel)
        {
            var modelId = Settings.ControlNetModels.NextId();
            ControlNetModel = controlNetModel.DeepClone(modelId);
            ControlNetModel.Name += " copy";
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(ControlNetModel[] modelImports)
        {
            var modelId = Settings.ControlNetModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                ControlNetModel = modelImport;
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.ControlNetModels.Any(x => x.Name == modelImport.Name && x.Pipeline == modelImport.Pipeline))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.ControlNetModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Models Imported.");
                return true;
            }
        }


        protected override async Task SaveAsync()
        {
            var index = Settings.ControlNetModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ControlNetModels.IndexOf(_originalControlNetModel);
                Settings.ControlNetModels.Remove(_originalControlNetModel);
            }
            Settings.ControlNetModels.Insert(index, ControlNetModel);

            ControlNetModel.Initialize(Settings);
            await SettingsManager.SaveAsync(Settings);
            await base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (ControlNetModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            ControlNetModel = default;
            _originalControlNetModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(ControlNetModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.ControlNetModels.Any(x => x.Name.Equals(ControlNetModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{ControlNetModel.Name}' already exists";
            }

            // Checkpoint
            if (!ControlNetModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
            if (string.IsNullOrEmpty(ControlNetModel.Checkpoint.MainFile))
                yield return "MainFile cannot be empty";
        }
    }
}
