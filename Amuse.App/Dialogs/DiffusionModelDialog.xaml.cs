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
    /// Interaction logic for DiffusionModelDialog.xaml
    /// </summary>
    public partial class DiffusionModelDialog : DialogControl
    {
        private SizeOption _selectedSize;
        private int _newWidth;
        private int _newHeight;
        private bool _newIsDefault;
        private DiffusionModel _diffusionModel;
        private DiffusionModel _originalDiffusionModel;
        private DiffusionCheckpointModel _checkpointModel;
        private SamplerInputOptions[] _samplers;
        private DataType[] _checkpointTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffusionModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public DiffusionModelDialog(Settings settings)
        {
            Settings = settings;
            Sizes = new ObservableCollection<SizeOption>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddCheckpointCommand = new AsyncRelayCommand(AddCheckpointAsync, CanAddCheckpoint);
            RemoveCheckpointCommand = new AsyncRelayCommand(RemoveCheckpointAsync, CanRemoveCheckpoint);
            AddSizeCommand = new AsyncRelayCommand(AddSizeAsync, CanAddSize);
            RemoveSizeCommand = new AsyncRelayCommand<SizeOption>(RemoveSizeAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public AsyncRelayCommand AddCheckpointCommand { get; }
        public AsyncRelayCommand RemoveCheckpointCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddSizeCommand { get; }
        public AsyncRelayCommand<SizeOption> RemoveSizeCommand { get; }
        public ObservableCollection<SizeOption> Sizes { get; }
        public bool IsUpdateMode => _originalDiffusionModel is not null;
        /// <summary>
        /// Gets or sets the data types a checkpoint set can be labelled with.
        /// </summary>
        public DataType[] CheckpointTypes
        {
            get { return _checkpointTypes; }
            set { SetProperty(ref _checkpointTypes, value); }
        }

        public DiffusionModel DiffusionModel
        {
            get { return _diffusionModel; }
            set { SetProperty(ref _diffusionModel, value); }
        }

        public DiffusionCheckpointModel CheckpointModel
        {
            get { return _checkpointModel; }
            set
            {
                SetProperty(ref _checkpointModel, value);
                AddCheckpointCommand.RaiseCanExecuteChanged();
                RemoveCheckpointCommand.RaiseCanExecuteChanged();
            }
        }

        public SizeOption SelectedSize
        {
            get { return _selectedSize; }
            set { SetProperty(ref _selectedSize, value); SetNewSize(value); }
        }

        public int NewWidth
        {
            get { return _newWidth; }
            set { SetProperty(ref _newWidth, value); }
        }

        public int NewHeight
        {
            get { return _newHeight; }
            set { SetProperty(ref _newHeight, value); }
        }

        public bool NewIsDefault
        {
            get { return _newIsDefault; }
            set { SetProperty(ref _newIsDefault, value); }
        }

        public SamplerInputOptions[] Samplers
        {
            get { return _samplers; }
            set { SetProperty(ref _samplers, value); }
        }


        /// <summary>
        /// Shows the dialog to update a model.
        /// </summary>
        /// <param name="diffusionModel">The model to update.</param>
        public Task<bool> UpdateAsync(DiffusionModel diffusionModel)
        {
            var modelId = diffusionModel.Id;
            _originalDiffusionModel = diffusionModel;
            DiffusionModel = diffusionModel.DeepClone(modelId);
            DiffusionModel.IsDefault = diffusionModel.IsDefault;
            DiffusionModel.IsNew = diffusionModel.IsNew;
            DiffusionModel.IsRecommended = diffusionModel.IsRecommended;
            DiffusionModel.UserMemoryMode = diffusionModel.UserMemoryMode;
            DiffusionModel.UserExecutionOptions = diffusionModel.UserExecutionOptions;
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of a model.
        /// </summary>
        /// <param name="diffusionModel">The model to copy.</param>
        public Task<bool> CopyAsync(DiffusionModel diffusionModel)
        {
            var modelId = Settings.DiffusionModels.NextId();
            DiffusionModel = diffusionModel.DeepClone(modelId);
            DiffusionModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports models, showing the dialog for a single model and adding several directly.
        /// </summary>
        /// <param name="modelImports">The models to import.</param>
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

                    if (!modelImport.GetComponents().Any())
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.DiffusionModels.Add(modelImport);
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
            DiffusionModel.Name = DiffusionModel.Name.Trim();
            DiffusionModel.Resolutions = [.. Sizes];
            DiffusionModel.ProcessTypes = GetProcessTypes();
            DiffusionModel.ViewFilter = GetViewFilter();
            if (DiffusionModel.Checkpoints.All(x => x.Type != DiffusionModel.CurrentCheckpointType))
                DiffusionModel.CurrentCheckpointType = null;


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


        /// <summary>
        /// Determines whether the model can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (DiffusionModel == null)
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
            DiffusionModel = default;
            _originalDiffusionModel = null;
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
        /// Adds the entered resolution, or changes the default of the resolution when it is listed, only one resolution is the default.
        /// </summary>
        private Task AddSizeAsync()
        {
            if (!CanAddSize())
                return Task.CompletedTask;

            if (_newIsDefault)
            {
                foreach (var size in Sizes)
                    size.IsDefault = false;
            }

            var existingSize = FindSize(_newWidth, _newHeight);
            if (existingSize is not null)
            {
                existingSize.IsDefault = _newIsDefault;
                return Task.CompletedTask;
            }

            Sizes.Add(new SizeOption
            {
                Width = _newWidth,
                Height = _newHeight,
                IsDefault = _newIsDefault,
            });
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the entered resolution can be added, or a listed resolution's default changed.
        /// </summary>
        private bool CanAddSize()
        {
            if (_newWidth <= 0 || _newHeight <= 0)
                return false;

            var existingSize = FindSize(_newWidth, _newHeight);
            return existingSize is null || existingSize.IsDefault != _newIsDefault;
        }


        /// <summary>
        /// Finds a listed resolution, <c>null</c> when it is not listed.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        private SizeOption FindSize(int width, int height)
        {
            return Sizes.FirstOrDefault(x => x.Width == width && x.Height == height);
        }


        /// <summary>
        /// Fills the entered resolution from a selected resolution.
        /// </summary>
        /// <param name="sizeOption">The selected resolution, nothing changes when <c>null</c>.</param>
        private void SetNewSize(SizeOption sizeOption)
        {
            if (sizeOption is null)
                return;

            NewWidth = sizeOption.Width;
            NewHeight = sizeOption.Height;
            NewIsDefault = sizeOption.IsDefault;
        }


        /// <summary>
        /// Removes a resolution.
        /// </summary>
        /// <param name="sizeOption">The resolution.</param>
        private Task RemoveSizeAsync(SizeOption sizeOption)
        {
            Sizes.Remove(sizeOption);
            return Task.CompletedTask;
        }


        /// <summary>
        /// Fills the functions, view filters and quality sets from the model.
        /// </summary>
        private void Populate()
        {
            var dataTypes = DiffusionModel.Backend.GetDataTypes();

            foreach (var size in DiffusionModel.Resolutions)
                Sizes.Add(size);

            if (DiffusionModel.DefaultOptions.Samplers != null)
                Samplers = DiffusionModel.DefaultOptions.Samplers.Copy();

            SetViewFilters();
            SetProcessTypes();
            SelectedSize = Sizes.FirstOrDefault(x => x.IsDefault) ?? Sizes.FirstOrDefault();
            CheckpointTypes = [.. new[] { DataType.Default }.Concat(dataTypes).Concat(DiffusionModel.Checkpoints.Select(x => x.Type)).Distinct()];
            CheckpointModel = DiffusionModel.Checkpoint;
            NotifyPropertyChanged(nameof(IsUpdateMode));
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(DiffusionModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.DiffusionModels.Any(x => x != _originalDiffusionModel && x.Name.Equals(DiffusionModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, DiffusionModel.Name.Trim());

            if (DiffusionModel.MediaType == MediaType.Image)
            {
                if (!Sizes.Any())
                    yield return AppErrors.ValidationResolutionsEmpty;
                if (!Sizes.Any(x => x.IsDefault))
                    yield return AppErrors.ValidationDefaultResolutionNotSet;
                if (DiffusionModel.DefaultOptions.Steps < 1)
                    yield return string.Format(AppErrors.ValidationGreaterThanZero, AppDefault.Steps);
            }
            else if (DiffusionModel.MediaType == MediaType.Video)
            {
                if (!Sizes.Any())
                    yield return AppErrors.ValidationResolutionsEmpty;
                if (!Sizes.Any(x => x.IsDefault))
                    yield return AppErrors.ValidationDefaultResolutionNotSet;
                if (DiffusionModel.DefaultOptions.Steps < 1)
                    yield return string.Format(AppErrors.ValidationGreaterThanZero, AppDefault.Steps);
                if (DiffusionModel.DefaultOptions.Frames < 1)
                    yield return string.Format(AppErrors.ValidationGreaterThanZero, Default.Frames);
                if (DiffusionModel.DefaultOptions.FrameRate < 1)
                    yield return string.Format(AppErrors.ValidationGreaterThanZero, Default.FrameRate);
            }
            else if (DiffusionModel.MediaType == MediaType.Audio)
            {
                if (DiffusionModel.DefaultOptions.SampleRate < 1)
                    yield return string.Format(AppErrors.ValidationGreaterThanZero, AppDefault.SampleRate);
                if (DiffusionModel.DefaultOptions.Steps < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Steps);
            }

            if (DiffusionModel.DefaultOptions.Steps2 < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Steps2);
            if (DiffusionModel.DefaultOptions.GuidanceScale < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.GuidanceCFG);
            if (DiffusionModel.DefaultOptions.GuidanceScale2 < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.GuidanceModel);
            if (DiffusionModel.DefaultOptions.Strength < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Strength);
            if (DiffusionModel.DefaultOptions.Channels < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, Default.Channels);
            if (DiffusionModel.DefaultOptions.MinLength < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MinLength);
            if (DiffusionModel.DefaultOptions.MaxLength < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MaxLength);
            if (DiffusionModel.DefaultOptions.MaxLength2 < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MaxLength2);
            if (DiffusionModel.DefaultOptions.Duration < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Duration);
            if (DiffusionModel.DefaultOptions.SilenceDuration < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.SilenceDuration);
            if (DiffusionModel.DefaultOptions.FrameChunkOverlap < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.FrameChunkOverlap);
            if (DiffusionModel.DefaultOptions.NoiseCondition < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.NoiseCondition);
            if (DiffusionModel.DefaultOptions.FrameChunk < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.FrameChunk);
            if (DiffusionModel.DefaultOptions.NoRepeatNgramSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.NoRepeatNgrams);
            if (DiffusionModel.DefaultOptions.Beams < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Beams);
            if (DiffusionModel.DefaultOptions.TopP < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.TopP);
            if (DiffusionModel.DefaultOptions.ChunkSize < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.ChunkSize);
            if (DiffusionModel.DefaultOptions.MaxLength > 0 && DiffusionModel.DefaultOptions.MinLength > DiffusionModel.DefaultOptions.MaxLength)
                yield return string.Format(AppErrors.ValidationLessOrEqual, AppDefault.MinLength, AppDefault.MaxLength);
            if (DiffusionModel.DefaultOptions.FrameChunk > 0 && DiffusionModel.DefaultOptions.FrameChunkOverlap >= DiffusionModel.DefaultOptions.FrameChunk)
                yield return string.Format(AppErrors.ValidationLessThan, AppDefault.FrameChunkOverlap, AppDefault.FrameChunk);
            if (DiffusionModel.DefaultOptions.MaxFrames < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MaxFrames);
            if (DiffusionModel.DefaultOptions.BPM < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.BPM);
            if (DiffusionModel.DefaultOptions.Speed < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Speed);
            if (DiffusionModel.DefaultOptions.TopK < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.TopK);
            if (DiffusionModel.DefaultOptions.Temperature < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.Temperature);
            if (DiffusionModel.DefaultOptions.TopP > 1)
                yield return string.Format(AppErrors.ValidationBetween, AppDefault.TopP, 0, 1);
            if (DiffusionModel.DefaultOptions.TypicalP < 0 || DiffusionModel.DefaultOptions.TypicalP > 1)
                yield return string.Format(AppErrors.ValidationBetween, AppDefault.TypicalP, 0, 1);
            if (DiffusionModel.DefaultOptions.RepetitionPenalty < 0)
                yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.RepetitionPenalty);

            var processTypes = GetProcessTypes();
            if (processTypes.IsNullOrEmpty())
                yield return AppErrors.ValidationProcessTypesEmpty;

            if (DiffusionModel.Checkpoints.Count == 0 || DiffusionModel.Checkpoints.Any(x => !x.GetComponents().Any()))
                yield return AppErrors.ValidationCheckpointEmpty;

            foreach (var checkpointSet in DiffusionModel.Checkpoints)
            {
                if (checkpointSet.MemorySize < 0)
                    yield return string.Format(AppErrors.ValidationZeroOrGreater, AppDefault.MemorySize);

                foreach (var checkpoint in checkpointSet.GetComponents())
                {
                    if (!checkpoint.IsValid(out var checkpointValidation))
                        yield return DiffusionModel.HasCheckpointSets
                            ? $"{checkpointSet.Type.GetDisplayName()} {checkpoint.Name} {checkpointValidation}"
                            : $"{checkpoint.Name} {checkpointValidation}";
                }
            }
            if (DiffusionModel.Checkpoints.Select(x => x.Type).Distinct().Count() != DiffusionModel.Checkpoints.Count)
                yield return AppErrors.ValidationQuantTypeExists;
        }


        /// <summary>
        /// Checks the supported functions of the model.
        /// </summary>
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


        /// <summary>
        /// Gets the checked supported functions.
        /// </summary>
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
                if (CheckBoxImageControlNet.IsChecked == true)
                    yield return ProcessType.ImageControlNet;
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


        /// <summary>
        /// Gets the checked views, <c>null</c> when none are checked.
        /// </summary>
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
                if (CheckBoxViewAudioToMusic.IsChecked == true)
                    yield return View.AudioToMusic;
                if (CheckBoxViewTextToAudio.IsChecked == true)
                    yield return View.TextToAudio;
                if (CheckBoxViewVoiceClone.IsChecked == true)
                    yield return View.VoiceClone;

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
                if (viewType == View.AudioToMusic)
                    CheckBoxViewAudioToMusic.IsChecked = true;
                if (viewType == View.TextToAudio)
                    CheckBoxViewTextToAudio.IsChecked = true;
                if (viewType == View.VoiceClone)
                    CheckBoxViewVoiceClone.IsChecked = true;

                if (viewType == View.ImageToText)
                    CheckBoxViewImageToText.IsChecked = true;
                if (viewType == View.TextInstruct)
                    CheckBoxViewTextInstruct.IsChecked = true;
                if (viewType == View.TextConverse)
                    CheckBoxViewTextConverse.IsChecked = true;
            }
        }

        /// <summary>
        /// Adds a checkpoint set copied from the selected one, with the first type not already used.
        /// </summary>
        private Task AddCheckpointAsync()
        {
            var checkpoint = _checkpointModel.DeepClone();
            checkpoint.Type = _checkpointTypes.FirstOrDefault(x => DiffusionModel.Checkpoints.All(c => c.Type != x));
            DiffusionModel.Checkpoints = [.. DiffusionModel.Checkpoints, checkpoint];
            CheckpointModel = checkpoint;
            RemoveCheckpointCommand.RaiseCanExecuteChanged();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether there is a selected checkpoint set to copy.
        /// </summary>
        private bool CanAddCheckpoint()
        {
            return _checkpointModel is not null && _checkpointTypes?.Any(x => DiffusionModel.Checkpoints.All(c => c.Type != x)) == true;
        }


        /// <summary>
        /// Removes the selected checkpoint set and selects the first one left.
        /// </summary>
        private Task RemoveCheckpointAsync()
        {
            DiffusionModel.Checkpoints = [.. DiffusionModel.Checkpoints.Where(x => x != _checkpointModel)];
            CheckpointModel = DiffusionModel.Checkpoints.FirstOrDefault();
            RemoveCheckpointCommand.RaiseCanExecuteChanged();
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the selected checkpoint set can be removed, a model keeps at least one.
        /// </summary>
        private bool CanRemoveCheckpoint()
        {
            return _checkpointModel is not null && DiffusionModel?.Checkpoints.Count > 1;
        }
    }
}
