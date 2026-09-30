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
    /// Interaction logic for LoraModelDialog.xaml
    /// </summary>
    public partial class LoraModelDialog : DialogControl
    {
        private LoraAdapterModel _loraModel;
        private LoraAdapterModel _originalLoraModel;
        private string _selectedTrigger;

        public LoraModelDialog(Settings settings)
        {
            Settings = settings;
            Trigger = new ObservableCollection<string>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            AddTriggerCommand = new AsyncRelayCommand(AddTriggerAsync, CanAddTrigger);
            RemoveTriggerCommand = new AsyncRelayCommand<string>(RemoveTriggerAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public AsyncRelayCommand AddTriggerCommand { get; }
        public AsyncRelayCommand<string> RemoveTriggerCommand { get; }
        public ObservableCollection<string> Trigger { get; }
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile];
        public bool IsUpdateMode => _originalLoraModel is not null;

        public LoraAdapterModel LoraModel
        {
            get { return _loraModel; }
            set { SetProperty(ref _loraModel, value); }
        }

        public string SelectedTrigger
        {
            get { return _selectedTrigger; }
            set { SetProperty(ref _selectedTrigger, value); }
        }

        public async Task<bool> AddAsync()
        {
            return await AddAsync(Settings.DiffusionPipelines.First());
        }


        public Task<bool> AddAsync(PipelineType pipelineType)
        {
            var modelId = Settings.LoraAdapterModels.NextId();
            LoraModel = new LoraAdapterModel
            {
                Id = modelId,
                Pipeline = pipelineType,
                Name = "New Lora",
                Status = ModelStatusType.Unknown,
                Checkpoint = new CheckpointComponent
                {
                    Name = "LoraAdapter",
                    Type = CheckpointType.LocalFile
                }
            };
            return base.ShowDialogAsync();
        }


        public Task<bool> UpdateAsync(LoraAdapterModel loraModel)
        {
            var modelId = loraModel.Id;
            _originalLoraModel = loraModel;
            LoraModel = loraModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        public Task<bool> CopyAsync(LoraAdapterModel loraModel)
        {
            var modelId = Settings.LoraAdapterModels.NextId();
            LoraModel = loraModel.DeepClone(modelId);
            LoraModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        public async Task<bool> ImportAsync(LoraAdapterModel[] modelImports)
        {
            var modelId = Settings.LoraAdapterModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                LoraModel = modelImport;
                Populate();
                return await base.ShowDialogAsync();
            }
            else
            {
                var imported = 0;
                foreach (var modelImport in modelImports)
                {
                    if (Settings.LoraAdapterModels.Any(x => x.Name == modelImport.Name && x.Pipeline == modelImport.Pipeline))
                        continue;

                    imported++;
                    modelImport.Id = modelId++;
                    Settings.LoraAdapterModels.Add(modelImport);
                }

                await DialogService.ShowMessageAsync("Import Complete", $"{imported}/{modelImports.Length} Lora Adapters Imported.");
                return true;
            }
        }


        protected override async Task SaveAsync()
        {
            LoraModel.ViewFilter = GetViewFilter();
            var index = Settings.LoraAdapterModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.LoraAdapterModels.IndexOf(_originalLoraModel);
                Settings.LoraAdapterModels.Remove(_originalLoraModel);
            }

            LoraModel.Key = CreateKey();
            LoraModel.Triggers = Trigger.Count == 0 ? default : Trigger.ToArray();
            Settings.LoraAdapterModels.Insert(index, LoraModel);
            LoraModel.Initialize(Settings);
            await SettingsManager.SaveAsync(Settings);
            await base.SaveAsync();
        }


        protected override bool CanExecuteSave()
        {
            if (LoraModel == null)
                return false;

            Errors.Clear();
            foreach (var inputError in GetValidationErrors())
                Errors.Add(inputError);

            return Errors.Count == 0 && base.CanExecuteSave();
        }


        protected override Task CancelAsync()
        {
            LoraModel = default;
            _originalLoraModel = null;
            return base.CancelAsync();
        }


        protected override async Task CloseAsync()
        {
            await base.CloseAsync();
        }


        private Task AddTriggerAsync()
        {
            Trigger.Add(_selectedTrigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        private bool CanAddTrigger()
        {
            return !Trigger.Contains(_selectedTrigger);
        }


        private Task RemoveTriggerAsync(string trigger)
        {
            Trigger.Remove(trigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        private void Populate()
        {
            SetViewFilters();
            if (!LoraModel.Triggers.IsNullOrEmpty())
            {
                foreach (var trigger in LoraModel.Triggers)
                {
                    Trigger.Add(trigger);
                }
            }
        }


        private IEnumerable<string> GetValidationErrors()
        {
            // Name
            if (string.IsNullOrWhiteSpace(LoraModel.Name))
                yield return "Name cannot be empty";
            if (!IsUpdateMode)
            {
                if (Settings.LoraAdapterModels.Any(x => x.Name.Equals(LoraModel.Name, StringComparison.OrdinalIgnoreCase)))
                    yield return $"Model with Name '{LoraModel.Name}' already exists";
            }

            // Checkpoint
            if (!LoraModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }


        private string CreateKey()
        {
            return $"{new string([.. LoraModel.Name.Where(char.IsLetterOrDigit)])}{LoraModel.Id}".ToLower();
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
            }

            var viewFilters = ViewFilters().ToArray();
            if (viewFilters.IsNullOrEmpty())
                return null;

            return viewFilters;
        }


        private void SetViewFilters()
        {
            if (LoraModel.ViewFilter.IsNullOrEmpty())
                return;

            foreach (var viewType in LoraModel.ViewFilter)
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
            }
        }
    }
}
