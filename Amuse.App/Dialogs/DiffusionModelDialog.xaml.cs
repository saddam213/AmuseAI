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
    /// Interaction logic for DiffusionModelDialog.xaml
    /// </summary>
    public partial class DiffusionModelDialog : DialogControl
    {
        private SizeOption _selectedSize;
        private DiffusionModel _diffusionModel;
        private DiffusionModel _originalDiffusionModel;
        private DiffusionCheckpointModel _checkpointModel;
        private SchedulerInputOptions[] _schedulers;
        private DataType[] _dataTypes;

        public DiffusionModelDialog(Settings settings)
        {
            Settings = settings;
            Sizes = new ObservableCollection<SizeOption>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddSizeCommand = new AsyncRelayCommand(AddSizeAsync, CanAddSize);
            RemoveSizeCommand = new AsyncRelayCommand<SizeOption>(RemoveSizeAsync);
            Errors = new ObservableCollection<string>();
            AccessTokens = [new AccessToken("None", null), .. settings.AccessTokens.Select(x => new AccessToken(x.Name, x.Name))];
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
        /// <summary>
        /// Gets or sets the data types the model backend can load.
        /// </summary>
        public DataType[] DataTypes
        {
            get { return _dataTypes; }
            set { SetProperty(ref _dataTypes, value); }
        }
        public AccessToken[] AccessTokens { get; }

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


        public Task<bool> UpdateAsync(DiffusionModel diffusionModel)
        {
            var modelId = diffusionModel.Id;
            _originalDiffusionModel = diffusionModel;
            DiffusionModel = diffusionModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(DiffusionModel diffusionModel)
        {
            var modelId = Settings.DiffusionModels.NextId();
            DiffusionModel = diffusionModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(DiffusionModel[] modelImports)
        {
            var modelId = Settings.DiffusionModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                DiffusionModel = modelImport;
                Populate();
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.DiffusionModels.Any(x => x.Backend == modelImport.Backend && x.Name == modelImport.Name))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.DiffusionModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Models Imported.");
                return true;
            }
        }


        protected override Task SaveAsync()
        {
            DiffusionModel.Resolutions = [.. Sizes];
            DiffusionModel.ProcessTypes = GetProcessTypes();
            DiffusionModel.ViewFilter = GetViewFilter();

            var defaultSize = Sizes.FirstOrDefault(x => x.IsDefault);
            if (defaultSize != null)
            {
                DiffusionModel.DefaultOptions.Width = defaultSize.Width;
                DiffusionModel.DefaultOptions.Height = defaultSize.Height;
            }

            DiffusionModel.Initialize(Settings);

            var index = Settings.DiffusionModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.DiffusionModels.IndexOf(_originalDiffusionModel);
                Settings.DiffusionModels.Remove(_originalDiffusionModel);
            }
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
            // DataTypes, keep the current type if the backend list does not have it
            var dataTypes = DiffusionModel.Backend.GetDataTypes();
            DataTypes = dataTypes.Contains(DiffusionModel.BaseType) ? dataTypes : [.. dataTypes, DiffusionModel.BaseType];

            foreach (var size in DiffusionModel.Resolutions)
                Sizes.Add(size);

            if (DiffusionModel.DefaultOptions.Schedulers != null)
                Schedulers = DiffusionModel.DefaultOptions.Schedulers.Copy();

            SetViewFilters();
            SetProcessTypes();
            SelectedSize = Sizes.FirstOrDefault(x => x.IsDefault) ?? Sizes.FirstOrDefault();
            CheckpointModel = DiffusionModel.Checkpoint;
            NotifyPropertyChanged(nameof(AccessTokens));
            NotifyPropertyChanged(nameof(IsUpdateMode));
            DiffusionModel.NotifyPropertyChanged(nameof(DiffusionModel.AccessToken));
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(DiffusionModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.DiffusionModels.Any(x => x.Name.Equals(DiffusionModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{DiffusionModel.Name}' already exists";
            }

            if (DiffusionModel.MediaType == MediaType.Image)
            {
                if (!Sizes.Any())
                    yield return "Resolutions cannot be empty";
                if (!Sizes.Any(x => x.IsDefault))
                    yield return "Default resolutions is not set";
                if (DiffusionModel.DefaultOptions.Steps < 1)
                    yield return "Steps must be be > 0";
            }
            else if (DiffusionModel.MediaType == MediaType.Video)
            {
                if (!Sizes.Any())
                    yield return "Resolutions cannot be empty";
                if (!Sizes.Any(x => x.IsDefault))
                    yield return "Default resolutions is not set";
                if (DiffusionModel.DefaultOptions.Steps < 1)
                    yield return "Steps must be be > 0";
                if (DiffusionModel.DefaultOptions.Frames < 1)
                    yield return "Frames must be be > 0";
                if (DiffusionModel.DefaultOptions.FrameRate < 1)
                    yield return "FrameRate must be be > 0";
            }
            else if (DiffusionModel.MediaType == MediaType.Audio)
            {
                if (DiffusionModel.DefaultOptions.SampleRate < 1)
                    yield return "SampleRate must be be > 0";
                if (DiffusionModel.Pipeline == PipelineType.WhisperPipeline)
                {
                    if (DiffusionModel.DefaultOptions.TopK < 1)
                        yield return "TopK must be be > 0";
                }
                else
                {
                    if (DiffusionModel.DefaultOptions.Steps < 1)
                        yield return "Steps must be be > 0";
                }
            }

            if (DiffusionModel.DefaultOptions.Steps2 < 0)
                yield return "Steps2 must be be >= 0";
            if (DiffusionModel.DefaultOptions.GuidanceScale < 0)
                yield return "GuidanceScale must be be >= 0";
            if (DiffusionModel.DefaultOptions.GuidanceScale2 < 0)
                yield return "GuidanceScale2 must be be >= 0";
            if (DiffusionModel.DefaultOptions.Strength < 0)
                yield return "Strength must be be >= 0";
            if (DiffusionModel.DefaultOptions.Channels < 0)
                yield return "Channels must be be >= 0";
            if (DiffusionModel.DefaultOptions.MinLength < 0)
                yield return "MinLength must be be >= 0";
            if (DiffusionModel.DefaultOptions.MaxLength < 0)
                yield return "MaxLength must be be >= 0";
            if (DiffusionModel.DefaultOptions.MaxLength2 < 0)
                yield return "MaxLength2 must be be >= 0";
            if (DiffusionModel.DefaultOptions.Duration < 0)
                yield return "Duration must be be >= 0";
            if (DiffusionModel.DefaultOptions.SilenceDuration < 0)
                yield return "SilenceDuration must be be >= 0";
            if (DiffusionModel.DefaultOptions.FrameChunkOverlap < 0)
                yield return "FrameChunkOverlap must be be >= 0";
            if (DiffusionModel.DefaultOptions.NoiseCondition < 0)
                yield return "NoiseCondition must be be >= 0";
            if (DiffusionModel.DefaultOptions.FrameChunk < 0)
                yield return "FrameChunk must be be >= 0";
            if (DiffusionModel.DefaultOptions.NoRepeatNgramSize < 0)
                yield return "NoRepeatNgramSize must be be >= 0";
            if (DiffusionModel.DefaultOptions.Beams < 0)
                yield return "Beams must be be >= 0";
            if (DiffusionModel.DefaultOptions.TopP < 0)
                yield return "TopP must be be >= 0";
            if (DiffusionModel.DefaultOptions.ChunkSize < 0)
                yield return "ChunkSize must be be >= 0";

            // Memory
            if (DiffusionModel.MemorySize < 0)
                yield return "Memory Size must be >= 0";

            // ProcessTypes
            var processTypes = GetProcessTypes();
            if (processTypes.IsNullOrEmpty())
                yield return "ProcessTypes cannot be empty";

            // Checkpoint
            foreach (var checkpoint in DiffusionModel.Checkpoint.GetComponents())
            {
                if (!checkpoint.IsValid(out var checkpointValidation))
                    yield return $"{checkpoint.Name} {checkpointValidation}";
            }
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
                if (processType == ProcessType.TextToAudio)
                    CheckBoxTextToAudio.IsChecked = true;
                if (processType == ProcessType.AudioToText)
                    CheckBoxAudioToText.IsChecked = true;
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
                if (CheckBoxTextToAudio.IsChecked == true)
                    yield return ProcessType.TextToAudio;
                if (CheckBoxAudioToText.IsChecked == true)
                    yield return ProcessType.AudioToText;
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
                if (CheckBoxViewTextToImage.IsChecked == true)
                    yield return View.TextToImage;
                if (CheckBoxViewImageToImage.IsChecked == true)
                    yield return View.ImageToImage;
                if (CheckBoxViewImageEdit.IsChecked == true)
                    yield return View.ImageEdit;
                if (CheckBoxViewImageInpaint.IsChecked == true)
                    yield return View.ImageInpaint;
                if (CheckBoxViewPaintToImage.IsChecked == true)
                    yield return View.PaintToImage;
                if (CheckBoxViewFrameToFrame.IsChecked == true)
                    yield return View.FrameToFrame;

                if (CheckBoxViewTextToVideo.IsChecked == true)
                    yield return View.TextToVideo;
                if (CheckBoxViewImageToVideo.IsChecked == true)
                    yield return View.ImageToVideo;
                if (CheckBoxViewVideoToVideo.IsChecked == true)
                    yield return View.VideoToVideo;

                if (CheckBoxViewTextToMusic.IsChecked == true)
                    yield return View.TextToMusic;
                if (CheckBoxViewTextToAudio.IsChecked == true)
                    yield return View.TextToAudio;

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
            if (DiffusionModel.ViewFilter.IsNullOrEmpty())
                return;

            foreach (var viewType in DiffusionModel.ViewFilter)
            {
                if (viewType == View.TextToImage)
                    CheckBoxViewTextToImage.IsChecked = true;
                if (viewType == View.ImageToImage)
                    CheckBoxViewImageToImage.IsChecked = true;
                if (viewType == View.ImageEdit)
                    CheckBoxViewImageEdit.IsChecked = true;
                if (viewType == View.ImageInpaint)
                    CheckBoxViewImageInpaint.IsChecked = true;
                if (viewType == View.PaintToImage)
                    CheckBoxViewPaintToImage.IsChecked = true;
                if (viewType == View.FrameToFrame)
                    CheckBoxViewFrameToFrame.IsChecked = true;

                if (viewType == View.TextToVideo)
                    CheckBoxViewTextToVideo.IsChecked = true;
                if (viewType == View.ImageToVideo)
                    CheckBoxViewImageToVideo.IsChecked = true;
                if (viewType == View.VideoToVideo)
                    CheckBoxViewVideoToVideo.IsChecked = true;

                if (viewType == View.TextToMusic)
                    CheckBoxViewTextToMusic.IsChecked = true;
                if (viewType == View.TextToAudio)
                    CheckBoxViewTextToAudio.IsChecked = true;

                if (viewType == View.ImageToText)
                    CheckBoxViewImageToText.IsChecked = true;
                if (viewType == View.TextInstruct)
                    CheckBoxViewTextInstruct.IsChecked = true;
                if (viewType == View.TextConverse)
                    CheckBoxViewTextConverse.IsChecked = true;
            }
        }

        public record AccessToken(string Name, string Value);
    }
}
