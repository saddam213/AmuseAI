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
        private static readonly View[] _controlNetViews = [View.ImageToImage, View.PaintToImage];

        private ControlNetModel _controlNetModel;
        private ControlNetModel _originalControlNetModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlNetModelDialog"/> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        public ControlNetModelDialog(Settings settings)
        {
            Settings = settings;
            PipelineOptions = new ObservableCollection<PipelineType>(settings.GetControlNetPipelines());
            ViewOptions = new ObservableCollection<ViewFilterOption>();
            SaveCommand = new AsyncRelayCommand(SaveAsync, CanExecuteSave);
            CancelCommand = new AsyncRelayCommand(CancelAsync);
            Errors = new ObservableCollection<string>();
            InitializeComponent();
        }

        public Settings Settings { get; }
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand CancelCommand { get; }
        public ObservableCollection<string> Errors { get; }
        public ObservableCollection<PipelineType> PipelineOptions { get; }
        public ObservableCollection<ViewFilterOption> ViewOptions { get; }
        public CheckpointType[] CheckpointTypes { get; } = [CheckpointType.OnlineFile, CheckpointType.LocalFile];
        public string FileFilter { get; } = DownloadLink.GetFileFilter(AppDefault.ModelFiles, [".safetensors", ".gguf"]);
        public bool IsUpdateMode => _originalControlNetModel is not null;

        public ControlNetModel ControlNetModel
        {
            get { return _controlNetModel; }
            set { SetProperty(ref _controlNetModel, value); }
        }


        /// <summary>
        /// Shows the dialog to update a ControlNet.
        /// </summary>
        /// <param name="controlNetModel">The ControlNet to update.</param>
        public Task<bool> UpdateAsync(ControlNetModel controlNetModel)
        {
            var modelId = controlNetModel.Id;
            _originalControlNetModel = controlNetModel;
            ControlNetModel = controlNetModel.DeepClone(modelId);
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Shows the dialog to add a copy of a ControlNet.
        /// </summary>
        /// <param name="controlNetModel">The ControlNet to copy.</param>
        public Task<bool> CopyAsync(ControlNetModel controlNetModel)
        {
            var modelId = Settings.ControlNetModels.NextId();
            ControlNetModel = controlNetModel.DeepClone(modelId);
            ControlNetModel.Name += " copy";
            Populate();
            return base.ShowDialogAsync();
        }


        /// <summary>
        /// Imports ControlNets, showing the dialog for a single ControlNet and adding several directly.
        /// </summary>
        /// <param name="modelImports">The ControlNets to import.</param>
        public async Task<bool> ImportAsync(ControlNetModel[] modelImports)
        {
            var modelId = Settings.ControlNetModels.NextId();
            if (modelImports.Length == 1)
            {
                var modelImport = modelImports[0];
                modelImport.Id = modelId;
                ControlNetModel = modelImport;
                Populate();
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

                await DialogService.ShowMessageAsync(AppDefault.ImportCompleteTitle, string.Format(AppDefault.ImportCompleteModels, imported, modelImports.Length));
                return true;
            }
        }


        /// <summary>
        /// Saves the ControlNet, replacing the original when updating.
        /// </summary>
        protected override async Task SaveAsync()
        {
            ControlNetModel.Name = ControlNetModel.Name.Trim();
            ControlNetModel.ViewFilter = GetViewFilter();
            var index = Settings.ControlNetModels.Count;
            if (IsUpdateMode)
            {
                index = Settings.ControlNetModels.IndexOf(_originalControlNetModel);
                Settings.ControlNetModels.Remove(_originalControlNetModel);
            }

            Settings.ControlNetModels.Insert(index, ControlNetModel);
            ControlNetModel.Initialize(Settings);
            await Settings.SaveAsync();
            await base.SaveAsync();
        }


        /// <summary>
        /// Determines whether the ControlNet can be saved, listing the validation errors.
        /// </summary>
        protected override bool CanExecuteSave()
        {
            if (ControlNetModel == null)
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
            ControlNetModel = default;
            _originalControlNetModel = null;
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
        /// Fills the pipeline and views from the ControlNet.
        /// </summary>
        private void Populate()
        {
            if (!PipelineOptions.Contains(ControlNetModel.Pipeline))
            {
                PipelineOptions.Add(ControlNetModel.Pipeline);
                NotifyPropertyChanged(nameof(ControlNetModel));
            }

            var selected = ControlNetModel.ViewFilter ?? [];
            ViewOptions.Clear();
            foreach (var view in _controlNetViews.Union(selected))
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
            if (string.IsNullOrWhiteSpace(ControlNetModel.Name))
                yield return AppErrors.ValidationNameEmpty;
            else if (Settings.ControlNetModels.Any(x => x != _originalControlNetModel && x.Name.Equals(ControlNetModel.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                yield return string.Format(AppErrors.ValidationModelNameExists, ControlNetModel.Name.Trim());

            if (!ControlNetModel.Checkpoint.IsValid(out var checkpointValidation))
                yield return checkpointValidation;
        }
    }
}
