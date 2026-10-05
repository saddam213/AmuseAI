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
    /// Interaction logic for UpscaleModelDialog.xaml
    /// </summary>
    public partial class UpscaleModelDialog : DialogControl
    {
        private UpscaleModel _upscaleModel;
        private UpscaleModel _originalUpscaleModel;

        public UpscaleModelDialog(Settings settings)
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
        public bool IsUpdateMode => _originalUpscaleModel is not null;

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            set { SetProperty(ref _upscaleModel, value); }
        }


        public Task<bool> AddAsync()
        {
            var modelId = Settings.UpscaleModels.NextId();
            UpscaleModel = new UpscaleModel
            {
                Id = modelId,
                Backend = BackendType.OnnxRuntime,
                Pipeline = PipelineType.UpscalePipeline,
                Status = ModelStatusType.Unknown,
                Name = "New Upscaler",
                Checkpoint = new CheckpointComponent
                {
                    Name = "Upscale",
                    Type = CheckpointType.LocalFile
                },
                DefaultOptions = new UpscaleInputOptions
                {
                    IsTileEnabled = true,
                    TileOverlap = 16,
                    TileSize = 512
                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(UpscaleModel upscaleModel)
        {
            var modelId = upscaleModel.Id;
            _originalUpscaleModel = upscaleModel;
            UpscaleModel = upscaleModel.DeepClone(modelId);
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(UpscaleModel upscaleModel)
        {
            var modelId = Settings.UpscaleModels.NextId();
            UpscaleModel = upscaleModel.DeepClone(modelId);
            UpscaleModel.Name += " copy";
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(UpscaleModel[] modelImports)
        {
            var modelId = Settings.UpscaleModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                UpscaleModel = modelImport;
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.UpscaleModels.Any(x => x.Backend == modelImport.Backend && x.Name == modelImport.Name))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.UpscaleModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Models Imported.");
                return true;
            }
        }


        protected override async Task SaveAsync()
        {
            var index = Settings.UpscaleModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.UpscaleModels.IndexOf(_originalUpscaleModel);
                Settings.UpscaleModels.Remove(_originalUpscaleModel);
            }

            Settings.UpscaleModels.Insert(index, UpscaleModel);
            UpscaleModel.Initialize(Settings);
            await Settings.SaveAsync();
            await base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (UpscaleModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            UpscaleModel = default;
            _originalUpscaleModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(UpscaleModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.UpscaleModels.Any(x => x.Name.Equals(UpscaleModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{UpscaleModel.Name}' already exists";
            }

            // Options
            if (UpscaleModel.Channels < 3)
                yield return "Channels must be >= 3";
            if (UpscaleModel.ScaleFactor <= 0)
                yield return "Scale Factor must be > 0";
            if (UpscaleModel.SampleSize < 0)
                yield return "Sample Size must be >= 0";
            if (UpscaleModel.MemorySize < 0)
                yield return "Sample Size must be >= 0";

            //DefaultOptions
            if (UpscaleModel.DefaultOptions.TileSize < 0)
                yield return "TileSize Size must be >= 0";
            if (UpscaleModel.DefaultOptions.TileOverlap < 0)
                yield return "TileOverlap Size must be >= 0";

            // Checkpoint
            if (!UpscaleModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }
    }
}
