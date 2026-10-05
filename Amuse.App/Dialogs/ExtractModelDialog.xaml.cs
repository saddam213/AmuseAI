// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
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
    /// Interaction logic for ExtractModelDialog.xaml
    /// </summary>
    public partial class ExtractModelDialog : DialogControl
    {
        private ExtractModel _extractModel;
        private ExtractModel _originalExtractModel;

        public ExtractModelDialog(Settings settings)
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
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile];
        public bool IsUpdateMode => _originalExtractModel is not null;

        public ExtractModel ExtractModel
        {
            get { return _extractModel; }
            set { SetProperty(ref _extractModel, value); }
        }

        public Task<bool> AddAsync()
        {
            var modelId = Settings.ExtractModels.NextId();
            ExtractModel = new ExtractModel
            {
                Id = modelId,
                Backend = BackendType.OnnxRuntime,
                Pipeline = PipelineType.ExtractPipeline,
                Status = ModelStatusType.Unknown,
                Name = "New Extractor",
                Checkpoint = new CheckpointComponent
                {
                    Name = "Extract",
                    Type = CheckpointType.LocalFile
                },
                DefaultOptions = new ExtractInputOptions
                {
                    IsTileEnabled = true,
                    TileOverlap = 16,
                    TileSize = 512
                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(ExtractModel extractModel)
        {
            var modelId = extractModel.Id;
            _originalExtractModel = extractModel;
            ExtractModel = extractModel.DeepClone(modelId);
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(ExtractModel extractModel)
        {
            var modelId = Settings.ExtractModels.NextId();
            ExtractModel = extractModel.DeepClone(modelId);
            ExtractModel.Name += " copy";
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(ExtractModel[] modelImports)
        {
            var modelId = Settings.ExtractModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                ExtractModel = modelImport;
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.ExtractModels.Any(x => x.Backend == modelImport.Backend && x.Name == modelImport.Name))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.ExtractModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Models Imported.");
                return true;
            }
        }


        protected override async Task SaveAsync()
        {
            var index = Settings.ExtractModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ExtractModels.IndexOf(_originalExtractModel);
                Settings.ExtractModels.Remove(_originalExtractModel);
            }
            Settings.ExtractModels.Insert(index, ExtractModel);
            ExtractModel.Initialize(Settings);
            await Settings.SaveAsync();
            await base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (ExtractModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            ExtractModel = default;
            _originalExtractModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(ExtractModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.ExtractModels.Any(x => x.Name.Equals(ExtractModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{ExtractModel.Name}' already exists";
            }

            // Options
            if (ExtractModel.Channels <= 0)
                yield return "Channels must be > 0";
            if (ExtractModel.SampleSize < 0)
                yield return "Sample Size must be >= 0";
            if (ExtractModel.MemorySize < 0)
                yield return "Sample Size must be >= 0";

            //DefaultOptions
            if (ExtractModel.DefaultOptions.TileSize < 0)
                yield return "TileSize Size must be >= 0";
            if (ExtractModel.DefaultOptions.TileOverlap < 0)
                yield return "TileOverlap Size must be >= 0";

            // Checkpoint
            if (!ExtractModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }

    }
}
