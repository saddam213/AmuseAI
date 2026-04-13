// Copyright (c) TensorStack. All rights reserved.
// Licensed under the Apache 2.0 License.
using Amuse.App.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TensorStack.Common;
using TensorStack.Common.Common;
using TensorStack.Python.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for DiffusionModelDialog.xaml
    /// </summary>
    public partial class DiffusionModelDialog : DialogControl
    {
        private SizeOption _selectedSize;
        private DiffusionModel _diffusionModel;
        private DiffusionModel _originalDiffusionModel;
        private DiffusionCheckpointModel _checkpointModel;
        private string _frameOptions;
        private SchedulerInputOptions[] _schedulers;

        public DiffusionModelDialog(Settings settings)
        {
            Settings = settings;
            DataTypes = [DataType.Bfloat16, DataType.Float16, DataType.Float8, DataType.Int8];
            ModelSources = [ModelSourceType.HuggingFace, ModelSourceType.Folder, ModelSourceType.SingleFile, ModelSourceType.Checkpoint];
            Sizes = new ObservableCollection<SizeOption>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddSizeCommand = new AsyncRelayCommand(AddSizeAsync, CanAddSize);
            RemoveSizeCommand = new AsyncRelayCommand<SizeOption>(RemoveSizeAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddSizeCommand { get; }
        public AsyncRelayCommand<SizeOption> RemoveSizeCommand { get; }
        public ObservableCollection<SizeOption> Sizes { get; }
        public bool IsUpdateMode => _originalDiffusionModel is not null;
        public DataType[] DataTypes { get; }
        public ModelSourceType[] ModelSources { get; }

        public DiffusionModel DiffusionModel
        {
            get { return _diffusionModel; }
            set { SetProperty(ref _diffusionModel, value); }
        }

        public DiffusionCheckpointModel CheckpointModel
        {
            get { return _checkpointModel; }
            set { SetProperty(ref _checkpointModel, value); }
        }

        public SizeOption SelectedSize
        {
            get { return _selectedSize; }
            set { SetProperty(ref _selectedSize, value); }
        }

        public SchedulerInputOptions[] Schedulers
        {
            get { return _schedulers; }
            set { SetProperty(ref _schedulers, value); }
        }

        public string FrameOptions
        {
            get { return _frameOptions; }
            set { SetProperty(ref _frameOptions, value); }
        }


        public Task<bool> UpdateAsync(DiffusionModel diffusionModel)
        {
            var modelId = diffusionModel.Id;
            _originalDiffusionModel = diffusionModel;
            DiffusionModel = DeepClone(diffusionModel, modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(DiffusionModel diffusionModel)
        {
            var modelId = GetNextModelId();
            DiffusionModel = DeepClone(diffusionModel, modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> ImportAsync(DiffusionModel diffusionModel)
        {
            diffusionModel.Id = GetNextModelId();
            DiffusionModel = diffusionModel;
            Populate();
            return base.ShowDialogAsync();
        }


        protected override Task SaveAsync()
        {
            var index = Settings.DiffusionModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.DiffusionModels.IndexOf(_originalDiffusionModel);
                Settings.DiffusionModels.Remove(_originalDiffusionModel);
            }

            DiffusionModel.Resolutions = [.. Sizes];
            DiffusionModel.ProcessTypes = GetProcessTypes();

            var defaultSize = Sizes.FirstOrDefault(x => x.IsDefault);
            DiffusionModel.DefaultOptions.Width = defaultSize.Width;
            DiffusionModel.DefaultOptions.Height = defaultSize.Height; ;
            DiffusionModel.DefaultOptions.FrameOptions = GetFrameOptions(FrameOptions);

            if ((DiffusionModel.Source == ModelSourceType.HuggingFace || DiffusionModel.Source == ModelSourceType.Checkpoint || DiffusionModel.Source == ModelSourceType.SingleFile) && Utils.TryParseHuggingFaceRepo(DiffusionModel.Path, out var huggingfacePath))
                DiffusionModel.Path = huggingfacePath;

            if (DiffusionModel.Source == ModelSourceType.SingleFile)
            {
                _checkpointModel.TextEncoder= null;
                _checkpointModel.TextEncoder2 = null;
                _checkpointModel.TextEncoder3 = null;
                _checkpointModel.Transformer = null;
                _checkpointModel.Transformer2 = null;
                _checkpointModel.Vae = null;
                _checkpointModel.AudioVae = null;
                _checkpointModel.Vocoder = null;
                _checkpointModel.Connectors = null;
                DiffusionModel.Checkpoint = _checkpointModel;
            }
            if (DiffusionModel.Source == ModelSourceType.Checkpoint)
            {
                _checkpointModel.SingleFile = null;
                DiffusionModel.Checkpoint = _checkpointModel;
            }

            DiffusionModel.Initialize(Settings.DirectoryModel);
            Settings.DiffusionModels.Insert(index, DiffusionModel);
            return base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (DiffusionModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            DiffusionModel = default;
            _originalDiffusionModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private Task AddSizeAsync()
        {
            if (!CanAddSize())
                return Task.CompletedTask;

            if (SelectedSize.IsDefault)
            {
                foreach (var size in Sizes)
                    size.IsDefault = false;
            }

            Sizes.Add(new SizeOption
            {
                Width = SelectedSize.Width,
                Height = SelectedSize.Height,
                IsDefault = SelectedSize.IsDefault,
            });

            SelectedSize.IsDefault = false;
            NotifyPropertyChanged(nameof(SelectedSize));
            return Task.CompletedTask;
        }


        private bool CanAddSize()
        {
            return SelectedSize is not null
                && SelectedSize.Width > 0
                && SelectedSize.Height > 0
                && !Sizes.Any(x => x.Width == SelectedSize.Width && x.Height == SelectedSize.Height);
        }


        private Task RemoveSizeAsync(SizeOption sizeOption)
        {
            Sizes.Remove(sizeOption);
            return Task.CompletedTask;
        }


        private void Populate()
        {
            foreach (var size in DiffusionModel.Resolutions)
                Sizes.Add(size);

            Schedulers = DiffusionModel.DefaultOptions.Schedulers.GetSchedulers().Select(SchedulerInputOptions.Create).ToArray();

            SetProcessTypes();
            FrameOptions = GetFrameOptions(DiffusionModel.DefaultOptions.FrameOptions);
            SelectedSize = Sizes.FirstOrDefault(x => x.IsDefault) ?? Sizes.FirstOrDefault();
            CheckpointModel = DiffusionModel.Checkpoint ?? new DiffusionCheckpointModel();
            NotifyPropertyChanged(nameof(IsUpdateMode));
        }


        private int GetNextModelId()
        {
            return Math.Max(Utils.FixedIdRange, Settings.DiffusionModels.Max(x => x.Id)) + 1;
        }


        private static string GetFrameOptions(int[] frameOptions)
        {
            return frameOptions.IsNullOrEmpty() ? string.Empty : string.Join(",", frameOptions);
        }


        private static int[] GetFrameOptions(string frameOptions)
        {
            if (string.IsNullOrEmpty(frameOptions))
                return null;

            var frameOptionsArray = frameOptions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => int.TryParse(x, out _))
                .Select(int.Parse)
                .ToArray();

            if (frameOptionsArray.IsNullOrEmpty())
                return null;
            return frameOptionsArray;
        }


        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(DiffusionModel.Name))
                yield return "Name cannot be empty";
            if (string.IsNullOrWhiteSpace(DiffusionModel.Path))
                yield return "Path cannot be empty";
            if (string.IsNullOrWhiteSpace(DiffusionModel.Pipeline))
                yield return "Pipeline cannot be empty";
            if (!IsUpdateMode && Settings.DiffusionModels.Any(x => x.Name.Equals(DiffusionModel.Name, StringComparison.OrdinalIgnoreCase)))
                yield return $"Model with name '{DiffusionModel.Name}' already exists";

            if (string.IsNullOrWhiteSpace(DiffusionModel.Path))
                yield return string.Empty;
            if (!string.IsNullOrWhiteSpace(DiffusionModel.Path))
            {
                if (DiffusionModel.Source == ModelSourceType.Folder && !Directory.Exists(DiffusionModel.Path))
                    yield return "Model folder not found";
                else if (DiffusionModel.Source == ModelSourceType.SingleFile && (string.IsNullOrEmpty(CheckpointModel.SingleFile) || !IsCheckpointValid(CheckpointModel.SingleFile)))
                    yield return "Model file not found";
                else if ((DiffusionModel.Source == ModelSourceType.HuggingFace || DiffusionModel.Source == ModelSourceType.Checkpoint) && !Utils.TryParseHuggingFaceRepo(DiffusionModel.Path, out _))
                    yield return "HuggingFace repository not found";

                if (DiffusionModel.Source == ModelSourceType.Checkpoint)
                {
                    if (string.IsNullOrEmpty(CheckpointModel.TextEncoder) 
                     && string.IsNullOrEmpty(CheckpointModel.TextEncoder2)
                     && string.IsNullOrEmpty(CheckpointModel.TextEncoder3)
                     && string.IsNullOrEmpty(CheckpointModel.Transformer)
                     && string.IsNullOrEmpty(CheckpointModel.Transformer2)
                     && string.IsNullOrEmpty(CheckpointModel.Vae)
                     && string.IsNullOrEmpty(CheckpointModel.AudioVae)
                     && string.IsNullOrEmpty(CheckpointModel.Vocoder)
                     && string.IsNullOrEmpty(CheckpointModel.Connectors))
                        yield return "At least one checkpoint model required";

                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder) && !IsCheckpointValid(CheckpointModel.TextEncoder))
                        yield return "TextEncoder checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder2) && !IsCheckpointValid(CheckpointModel.TextEncoder2))
                        yield return "TextEncoder2 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.TextEncoder3) && !IsCheckpointValid(CheckpointModel.TextEncoder3))
                        yield return "TextEncoder3 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Transformer) && !IsCheckpointValid(CheckpointModel.Transformer))
                        yield return "Transformer checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Transformer2) && !IsCheckpointValid(CheckpointModel.Transformer2))
                        yield return "Transformer2 checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Vae) && !IsCheckpointValid(CheckpointModel.Vae))
                        yield return "Vae checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.AudioVae) && !IsCheckpointValid(CheckpointModel.AudioVae))
                        yield return "AudioVae checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Vocoder) && !IsCheckpointValid(CheckpointModel.Vocoder))
                        yield return "Vocoder checkpoint file not found";
                    if (!string.IsNullOrEmpty(CheckpointModel.Connectors) && !IsCheckpointValid(CheckpointModel.Connectors))
                        yield return "Connectors checkpoint file not found";
                }
            }

            if (DiffusionModel.DefaultOptions.Steps < 1)
                yield return "Steps must be be > 0";
            if (DiffusionModel.DefaultOptions.GuidanceScale < 0)
                yield return "GuidanceScale must be be >= 0";
            if (DiffusionModel.DefaultOptions.GuidanceScale2 < 0)
                yield return "GuidanceScale2 must be be >= 0";
            if (DiffusionModel.DefaultOptions.Frames < 0)
                yield return "Frames must be be >= 0";
            if (DiffusionModel.DefaultOptions.FrameRate < 0)
                yield return "FrameRate must be be >= 0";
            if (!Sizes.Any())
                yield return "Resolutions cannot be empty";
            if (!Sizes.Any(x => x.IsDefault))
                yield return "Default resolutions is not set";

            foreach (var profile in DiffusionModel.MemoryProfile)
            {
                if (profile.MemoryModes.Any(x => x < 0))
                    yield return "MemoryMode must be >= 0";
            }

            var processTypes = GetProcessTypes();
            if (processTypes.IsNullOrEmpty())
                yield return "ProcessTypes cannot be empty";
        }


        private bool IsCheckpointValid(string checkpoint)
        {
            return File.Exists(checkpoint) || Utils.IsCheckpointInstalled(Settings.DirectoryCache, checkpoint) || Utils.IsHuggingFaceLink(checkpoint);
        }


        private void SetProcessTypes()
        {
            foreach (var processType in DiffusionModel.ProcessTypes)
            {
                if (processType == ProcessType.TextToImage)
                    CheckBoxTextToImage.IsChecked = true;
                if (processType == ProcessType.ImageToImage)
                    CheckBoxImageToImage.IsChecked = true;
                if (processType == ProcessType.ImageEdit)
                    CheckBoxImageEdit.IsChecked = true;
                if (processType == ProcessType.ImageInpaint)
                    CheckBoxImageInpaint.IsChecked = true;
                if (processType == ProcessType.ImageControlNet)
                    CheckBoxImageControlNet.IsChecked = true;
                if (processType == ProcessType.ImageToImageControlNet)
                    CheckBoxImageToImageControlNet.IsChecked = true;
                if (processType == ProcessType.TextToVideo)
                    CheckBoxTextToVideo.IsChecked = true;
                if (processType == ProcessType.ImageToVideo)
                    CheckBoxImageToVideo.IsChecked = true;
                if (processType == ProcessType.VideoToVideo)
                    CheckBoxVideoToVideo.IsChecked = true;
            }
        }


        private ProcessType[] GetProcessTypes()
        {
            IEnumerable<ProcessType> ProcessTypes()
            {
                if (CheckBoxTextToImage.IsChecked == true)
                    yield return ProcessType.TextToImage;
                if (CheckBoxImageToImage.IsChecked == true)
                    yield return ProcessType.ImageToImage;
                if (CheckBoxImageEdit.IsChecked == true)
                    yield return ProcessType.ImageEdit;
                if (CheckBoxImageInpaint.IsChecked == true)
                    yield return ProcessType.ImageInpaint;
                if (CheckBoxImageToImage.IsChecked == true)
                    yield return ProcessType.ImageToImage;
                if (CheckBoxImageToImageControlNet.IsChecked == true)
                    yield return ProcessType.ImageToImageControlNet;
                if (CheckBoxTextToVideo.IsChecked == true)
                    yield return ProcessType.TextToVideo;
                if (CheckBoxImageToVideo.IsChecked == true)
                    yield return ProcessType.ImageToVideo;
                if (CheckBoxVideoToVideo.IsChecked == true)
                    yield return ProcessType.VideoToVideo;
            }
            return [.. ProcessTypes()];
        }


        private static DiffusionModel DeepClone(DiffusionModel diffusionModel, int modelId)
        {
            return new DiffusionModel
            {
                Id = modelId,
                Backend = diffusionModel.Backend,
                Name = diffusionModel.Name,
                Path = diffusionModel.Path,
                Variant = diffusionModel.Variant,
                Pipeline = diffusionModel.Pipeline,
                IsDefault = diffusionModel.IsDefault,
                BaseType = diffusionModel.BaseType,
                IsGated = diffusionModel.IsGated,
                Link = diffusionModel.Link,
                MemoryProfile = diffusionModel.MemoryProfile.Select(x => new MemoryProfile
                {
                    QualityMode = x.QualityMode,
                    MemoryModes = x.MemoryModes.ToArray(),
                }).ToArray(),
                ProcessTypes = [.. diffusionModel.ProcessTypes],
                Vendor = diffusionModel.Vendor.IsNullOrEmpty() ? null : [.. diffusionModel.Vendor],
                Source = diffusionModel.Source,
                Resolutions = [.. diffusionModel.Resolutions.Select(x => new SizeOption
                {
                    Height = x.Height,
                    Width = x.Width,
                    IsDefault = x.IsDefault,
                })],
                DefaultOptions = new DiffusionDefaultOptions
                {
                    Width = diffusionModel.DefaultOptions.Width,
                    Height = diffusionModel.DefaultOptions.Height,
                    Steps = diffusionModel.DefaultOptions.Steps,
                    Steps2 = diffusionModel.DefaultOptions.Steps2,
                    GuidanceScale = diffusionModel.DefaultOptions.GuidanceScale,
                    GuidanceScale2 = diffusionModel.DefaultOptions.GuidanceScale2,
                    Frames = diffusionModel.DefaultOptions.Frames,
                    FrameRate = diffusionModel.DefaultOptions.FrameRate,
                    SampleRate = diffusionModel.DefaultOptions.SampleRate,
                    FrameChunk = diffusionModel.DefaultOptions.FrameChunk,
                    FrameChunkOverlap = diffusionModel.DefaultOptions.FrameChunkOverlap,
                    FrameOptions = diffusionModel.DefaultOptions.FrameOptions?.ToArray(),
                    NoiseCondition = diffusionModel.DefaultOptions.NoiseCondition,
                    Scheduler = diffusionModel.DefaultOptions.Scheduler,
                    Schedulers = diffusionModel.DefaultOptions.Schedulers with { },
                    Strength = diffusionModel.DefaultOptions.Strength,
                    IsVaeSlicingEnabled = diffusionModel.DefaultOptions.IsVaeSlicingEnabled,
                    IsVaeTilingEnabled = diffusionModel.DefaultOptions.IsVaeTilingEnabled,
                },
                Checkpoint = diffusionModel.Checkpoint is null ? null : new DiffusionCheckpointModel
                {
                    SingleFile = diffusionModel.Checkpoint.SingleFile,
                    TextEncoder = diffusionModel.Checkpoint.TextEncoder,
                    TextEncoder2 = diffusionModel.Checkpoint.TextEncoder2,
                    TextEncoder3 = diffusionModel.Checkpoint.TextEncoder3,
                    Transformer = diffusionModel.Checkpoint.Transformer,
                    Transformer2 = diffusionModel.Checkpoint.Transformer2,
                    Vae = diffusionModel.Checkpoint.Vae,
                    AudioVae = diffusionModel.Checkpoint.AudioVae,
                    Vocoder = diffusionModel.Checkpoint.Vocoder,
                    Connectors = diffusionModel.Checkpoint.Connectors
                }
            };
        }
    }
}
