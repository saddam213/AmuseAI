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
using TensorStack.WPF.Services;

namespace Amuse.App.Dialogs
{
    /// <summary>
    /// Interaction logic for LoraModelDialog.xaml
    /// </summary>
    public partial class LoraModelDialog : DialogControl
    {
        private static readonly View[] _loraViews =
        [
            View.TextToImage, View.ImageToImage, View.ImageEdit, View.ImageInpaint, View.PaintToImage, View.FrameToFrame,
            View.TextToVideo, View.ImageToVideo, View.VideoToVideo, View.MediaToVideo,
            View.TextToMusic, View.AudioToMusic, View.TextToAudio, View.VoiceClone
        ];

        private LoraAdapterModel _loraModel;
        private LoraAdapterModel _originalLoraModel;
        private PipelineType? _selectedPipeline;
        private string _selectedTrigger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoraModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public LoraModelDialog(Settings settings)
        {
            Settings = settings;
            Trigger = new ObservableCollection<string>();
            PipelineOptions = new ObservableCollection<PipelineType>(settings.GetLoraPipelines());
            ViewOptions = new ObservableCollection<ViewFilterOption>();
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
        public ObservableCollection<PipelineType> PipelineOptions { get; }
        public ObservableCollection<ViewFilterOption> ViewOptions { get; }
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile];
        public bool IsUpdateMode => _originalLoraModel is not null;

        public LoraAdapterModel LoraModel
        {
            get { return _loraModel; }
            set { SetProperty(ref _loraModel, value); }
        }

        public PipelineType? SelectedPipeline
        {
            get { return _selectedPipeline; }
            set
            {
                SetProperty(ref _selectedPipeline, value);
                if (_loraModel != null && value.HasValue)
                    _loraModel.Pipeline = value.Value;

                SetViewOptions(GetViewFilter(), false);
            }
        }

        public string SelectedTrigger
        {
            get { return _selectedTrigger; }
            set { SetProperty(ref _selectedTrigger, value); }
        }


        /// <summary>
        /// Shows the dialog to update a LoRA.
        /// </summary>
        /// <param name="loraModel">The LoRA to update.</param>
        public Task<bool> UpdateAsync(LoraAdapterModel loraModel)
        {
            var modelId = loraModel.Id;
            _originalLoraModel = loraModel;
            LoraModel = loraModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of a LoRA.
        /// </summary>
        /// <param name="loraModel">The LoRA to copy.</param>
        public Task<bool> CopyAsync(LoraAdapterModel loraModel)
        {
            var modelId = Settings.LoraAdapterModels.NextId();
            LoraModel = loraModel.DeepClone(modelId);
            LoraModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports LoRAs, showing the dialog for a single LoRA and adding several directly.
        /// </summary>
        /// <param name="modelImports">The LoRAs to import.</param>
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

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteLoras, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the LoRA, replacing the original when updating.
        /// </summary>
        protected override async Task SaveAsync()
        {
            LoraModel.Name = LoraModel.Name.Trim();
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
            await Settings.SaveAsync();
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the LoRA can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (LoraModel == null)
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
            LoraModel = default;
            _originalLoraModel = null;
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
        /// Adds the typed trigger word.
        /// </summary>
        private Task AddTriggerAsync()
        {
            Trigger.Add(_selectedTrigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the typed trigger word can be added.
        /// </summary>
        private bool CanAddTrigger()
        {
            return !string.IsNullOrWhiteSpace(_selectedTrigger) && !Trigger.Contains(_selectedTrigger);
        }


        /// <summary>
        /// Removes a trigger word.
        /// </summary>
        /// <param name="trigger">The trigger word.</param>
        private Task RemoveTriggerAsync(string trigger)
        {
            Trigger.Remove(trigger);
            SelectedTrigger = null;
            return Task.CompletedTask;
        }


        /// <summary>
        /// Fills the pipeline, views and trigger words from the LoRA.
        /// </summary>
        private void Populate()
        {
            if (!PipelineOptions.Contains(LoraModel.Pipeline))
                PipelineOptions.Add(LoraModel.Pipeline);

            _selectedPipeline = LoraModel.Pipeline;
            NotifyPropertyChanged(nameof(SelectedPipeline));
            SetViewOptions(LoraModel.ViewFilter, true);
            if (!LoraModel.Triggers.IsNullOrEmpty())
            {
                foreach (var trigger in LoraModel.Triggers)
                {
                    Trigger.Add(trigger);
                }
            }
        }


        /// <summary>
        /// Lists the views the selected pipeline can run, keeping the selected views that are still listed.
        /// </summary>
        /// <param name="selectedViews">The selected views.</param>
        /// <param name="isSelectionKept">Whether selected views the pipeline cannot run are listed too, so saving does not drop them.</param>
        private void SetViewOptions(View[] selectedViews, bool isSelectionKept)
        {
            var selected = selectedViews ?? [];
            var views = GetPipelineViews(_selectedPipeline);
            if (isSelectionKept)
                views.UnionWith(selected);

            ViewOptions.Clear();
            foreach (var view in _loraViews.Where(views.Contains))
            {
                ViewOptions.Add(new ViewFilterOption
                {
                    View = view,
                    Name = AppDefault.ResourceManager.GetString($"Menu_{view}", AppDefault.Culture) ?? view.ToString(),
                    IsSelected = selected.Contains(view)
                });
            }
        }


        /// <summary>
        /// Gets the views the models of a pipeline can run, every view when the pipeline has no models.
        /// </summary>
        /// <param name="pipeline">The pipeline.</param>
        private HashSet<View> GetPipelineViews(PipelineType? pipeline)
        {
            var views = new HashSet<View>();
            if (pipeline.HasValue)
            {
                var models = Settings.DiffusionModels
                    .Concat(Settings.Templates?.DiffusionTemplates ?? [])
                    .Where(x => x.Pipeline == pipeline.Value);
                foreach (var model in models)
                {
                    var modelViews = (model.ProcessTypes ?? []).SelectMany(GetProcessViews);
                    if (!model.ViewFilter.IsNullOrEmpty())
                        modelViews = modelViews.Where(model.ViewFilter.Contains);

                    views.UnionWith(modelViews);
                }
            }

            if (views.Count == 0)
                views.UnionWith(_loraViews);

            return views;
        }


        /// <summary>
        /// Gets the views that run a process type.
        /// </summary>
        /// <param name="processType">The process type.</param>
        private static View[] GetProcessViews(ProcessType processType)
        {
            return processType switch
            {
                ProcessType.TextToImage or ProcessType.ImageControlNet => [View.TextToImage],
                ProcessType.ImageToImage or ProcessType.ImageToImageControlNet => [View.ImageToImage, View.PaintToImage],
                ProcessType.ImageEdit => [View.ImageEdit, View.FrameToFrame],
                ProcessType.ImageInpaint => [View.ImageInpaint],
                ProcessType.TextToVideo => [View.TextToVideo],
                ProcessType.ImageToVideo => [View.ImageToVideo],
                ProcessType.VideoToVideo => [View.VideoToVideo],
                ProcessType.MediaToVideo => [View.MediaToVideo],
                ProcessType.TextToAudio => [View.TextToMusic, View.AudioToMusic, View.TextToAudio, View.VoiceClone],
                _ => []
            };
        }


        /// <summary>
        /// Gets the selected views, <c>null</c> when none are selected.
        /// </summary>
        private View[] GetViewFilter()
        {
            var viewFilters = ViewOptions
                .Where(x => x.IsSelected)
                .Select(x => x.View)
                .ToArray();
            return viewFilters.Length == 0 ? null : viewFilters;
        }


        /// <summary>
        /// Gets the validation errors of the current input.
        /// </summary>
        private IEnumerable<string> GetValidationErrors()
        {
            if (string.IsNullOrWhiteSpace(LoraModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.LoraAdapterModels.Any(x => x != _originalLoraModel && x.Name.Equals(LoraModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, LoraModel.Name.Trim());

            if (!LoraModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }


        /// <summary>
        /// Creates the LoRA key from the letters and digits of its name and its id.
        /// </summary>
        private string CreateKey()
        {
            return $"{new string([.. LoraModel.Name.Where(char.IsLetterOrDigit)])}{LoraModel.Id}".ToLower();
        }
    }
}
