// Copyright (c) Adam Clark. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using Amuse.App.Resources;
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
    /// Interaction logic for UpscaleModelDialog.xaml
    /// </summary>
    public partial class UpscaleModelDialog : DialogControl
    {
        private UpscaleModel _upscaleModel;
        private UpscaleModel _originalUpscaleModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpscaleModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
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

        /// <summary>
        /// Gets the VAE checkpoint types, a VAE can be a shared component.
        /// </summary>
        public CheckpointType[] VaeCheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile, CheckpointType.Component];

        /// <summary>
        /// Gets the backends that support upscaling.
        /// </summary>
        public BackendType[] Backends { get; } = [BackendType.OnnxRuntime, BackendType.StableDiffusionCpp];

        /// <summary>
        /// Gets the local file picker filter, every file type an upscale backend can load.
        /// </summary>
        public string FileFilter { get; } = $"{AppDefault.ModelFiles}|*.onnx;*.gguf;*.pth;*.safetensors;";
        public bool IsUpdateMode => _originalUpscaleModel is not null;

        public UpscaleModel UpscaleModel
        {
            get { return _upscaleModel; }
            set { SetProperty(ref _upscaleModel, value); }
        }


        /// <summary>
        /// Shows the dialog to update an upscaler.
        /// </summary>
        /// <param name="upscaleModel">The upscaler to update.</param>
        public Task<bool> UpdateAsync(UpscaleModel upscaleModel)
        {
            var modelId = upscaleModel.Id;
            _originalUpscaleModel = upscaleModel;
            UpscaleModel = upscaleModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of an upscaler.
        /// </summary>
        /// <param name="upscaleModel">The upscaler to copy.</param>
        public Task<bool> CopyAsync(UpscaleModel upscaleModel)
        {
            var modelId = Settings.UpscaleModels.NextId();
            UpscaleModel = upscaleModel.DeepClone(modelId);
            UpscaleModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports upscalers, showing the dialog for a single upscaler and adding several directly.
        /// </summary>
        /// <param name="modelImports">The upscalers to import.</param>
        public async Task<bool> ImportAsync(UpscaleModel[] modelImports)
        {
            var modelId = Settings.UpscaleModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                UpscaleModel = modelImport;
                Populate();
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

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteModels, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the upscaler, replacing the original when updating.
        /// </summary>
        protected override async Task SaveAsync()
        {
            UpscaleModel.Name = UpscaleModel.Name.Trim();
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


        /// <summary>
        /// Determines whether the upscaler can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (UpscaleModel == null)
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
            UpscaleModel = default;
            _originalUpscaleModel = null;
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
        /// Gives a video upscaler a VAE to fill in when it has none.
        /// </summary>
        private void Populate()
        {
            if (UpscaleModel.IsVideoUpscaler && UpscaleModel.Vae is null)
            {
                UpscaleModel.Vae = new CheckpointComponent
                {
                    Name = "VAE",
                    Type = CheckpointType.Component
                };
            }
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(UpscaleModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.UpscaleModels.Any(x => x != _originalUpscaleModel && x.Name.Equals(UpscaleModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, UpscaleModel.Name.Trim());

            // Options
            if (UpscaleModel.Channels < 3)
                yield return string.Format(AppErrors.ValidationMinimum, Default.Channels, 3);
            if (UpscaleModel.ScaleFactor <= 0)
                yield return string.Format(AppErrors.ValidationGreaterThanZero, AppDefault.ScaleFactor);
            if (UpscaleModel.SampleSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.SampleSize);
            if (UpscaleModel.MemorySize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MemorySize);

            //DefaultOptions
            if (UpscaleModel.DefaultOptions.TileSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, Default.TileSize);
            if (UpscaleModel.DefaultOptions.TileOverlap < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, Default.TileOverlap);

            // Checkpoint
            if (!UpscaleModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
            if (UpscaleModel.Vae is not null && !UpscaleModel.Vae.IsValid(out var vaeValidation))
                yield return vaeValidation;

            // Checkpoint file type must match the backend
            var supportedExtensions = GetSupportedExtensions(UpscaleModel);
            if (!IsSupportedFile(UpscaleModel.Checkpoint, supportedExtensions))
                yield return string.Format(AppErrors.UpscaleCheckpointFormat, string.Join(", ", supportedExtensions), UpscaleModel.Backend);
        }


        /// <summary>
        /// Gets the checkpoint file extensions the model's backend and pipeline can load.
        /// </summary>
        /// <param name="model">The model.</param>
        private static string[] GetSupportedExtensions(UpscaleModel model)
        {
            if (model.Backend == BackendType.OnnxRuntime)
                return [".onnx"];
            if (model.IsVideoUpscaler)
                return [".safetensors", ".gguf"];

            return [".gguf", ".pth", ".safetensors"];
        }


        /// <summary>
        /// Determines whether the checkpoint file has a supported extension, folders and components are not checked.
        /// </summary>
        /// <param name="checkpoint">The checkpoint.</param>
        /// <param name="supportedExtensions">The supported extensions.</param>
        private static bool IsSupportedFile(CheckpointComponent checkpoint, string[] supportedExtensions)
        {
            var fileName = checkpoint?.Type switch
            {
                CheckpointType.LocalFile => checkpoint.Path,
                CheckpointType.OnlineFile => checkpoint.DownloadFiles?.FirstOrDefault(),
                _ => null
            };
            if (string.IsNullOrEmpty(fileName))
                return true;

            var extension = System.IO.Path.GetExtension(fileName);
            return supportedExtensions.Any(x => x.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
