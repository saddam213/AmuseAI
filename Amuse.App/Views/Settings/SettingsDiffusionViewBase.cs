using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Resources;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using TensorStack.Common;
using TensorStack.WPF;
using TensorStack.WPF.Controls;
using TensorStack.WPF.Services;

namespace Amuse.App.Views
{
    /// <summary>
    /// Base class for the diffusion model settings views, filtered by MediaType.
    /// </summary>
    public abstract class SettingsDiffusionViewBase : ViewBase
    {
        private readonly MediaType _mediaType;
        private DiffusionModel _selectedModel;
        private string _filterText;
        private FilterOption _filterBackend;
        private FilterOption _filterPipeline;
        private FilterOption _filterStatus;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsDiffusionViewBase"/> class.
        /// </summary>
        /// <param name="mediaType">The MediaType of the models to show.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="navigationService">The navigation service.</param>
        /// <param name="downloadService">The download service.</param>
        /// <param name="historyService">The history service.</param>
        /// <param name="logger">The logger.</param>
        protected SettingsDiffusionViewBase(MediaType mediaType, Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            _mediaType = mediaType;
            FilterBackends = [FilterOption.ShowAll(), .. Enum.GetValues<BackendType>().Where(x => x != BackendType.HuggingFace).Select(x => FilterOption.FromEnum(x))];
            FilterStatuses = FilterOption.ModelStatuses();
            FilterPipelines = [FilterOption.ShowAll(), .. settings.DiffusionModels.Where(x => x.MediaType == mediaType).Select(x => x.Pipeline.ToString()).Distinct().Order().Select(FilterOption.FromValue)];
            FilterBackend = FilterBackends[0];
            FilterStatus = FilterStatuses[0];
            FilterPipeline = FilterPipelines[0];
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddModelWizardCommand = new AsyncRelayCommand(AddModelWizardAsync);
            CopyModelCommand = new AsyncRelayCommand(CopyModelAsync, () => SelectedModel is not null);
            UpdateModelCommand = new AsyncRelayCommand(UpdateModelAsync, () => SelectedModel?.Id > Utils.FixedIdRange);
            RemoveModelCommand = new AsyncRelayCommand(RemoveModelAsync, () => SelectedModel?.Id > Utils.FixedIdRange);
            ImportModelCommand = new AsyncRelayCommand(ImportModelAsync);
            ExportModelCommand = new AsyncRelayCommand(ExportModelAsync, () => SelectedModel is not null);
            DeleteModelCommand = new AsyncRelayCommand(DeleteModelAsync, () => SelectedModel is not null);
            OpenModelCommand = new AsyncRelayCommand(OpenModelAsync, () => SelectedModel is not null);
            DownloadModelCommand = new AsyncRelayCommand(DownloadModelAsync);
            DownloadModelCancelCommand = new AsyncRelayCommand(DownloadModelCancelAsync);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.DiffusionModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Backend), ListSortDirection.Descending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Name), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Status), ListSortDirection.Descending));
            ModelCollection.MoveCurrentToFirst();
            SelectedModel = ModelCollection.CurrentItem as DiffusionModel;
        }

        /// <summary>
        /// Gets the MediaType of the models shown.
        /// </summary>
        public MediaType MediaType => _mediaType;

        /// <summary>
        /// Gets the save command.
        /// </summary>
        public AsyncRelayCommand SaveCommand { get; }

        /// <summary>
        /// Gets the add model wizard command.
        /// </summary>
        public AsyncRelayCommand AddModelWizardCommand { get; }

        /// <summary>
        /// Gets the copy model command.
        /// </summary>
        public AsyncRelayCommand CopyModelCommand { get; }

        /// <summary>
        /// Gets the update model command.
        /// </summary>
        public AsyncRelayCommand UpdateModelCommand { get; }

        /// <summary>
        /// Gets the remove model command.
        /// </summary>
        public AsyncRelayCommand RemoveModelCommand { get; }

        /// <summary>
        /// Gets the import model command.
        /// </summary>
        public AsyncRelayCommand ImportModelCommand { get; }

        /// <summary>
        /// Gets the export model command.
        /// </summary>
        public AsyncRelayCommand ExportModelCommand { get; }

        /// <summary>
        /// Gets the delete model command.
        /// </summary>
        public AsyncRelayCommand DeleteModelCommand { get; }

        /// <summary>
        /// Gets the open model command.
        /// </summary>
        public AsyncRelayCommand OpenModelCommand { get; }

        /// <summary>
        /// Gets the download model command.
        /// </summary>
        public AsyncRelayCommand DownloadModelCommand { get; }

        /// <summary>
        /// Gets the download model cancel command.
        /// </summary>
        public AsyncRelayCommand DownloadModelCancelCommand { get; }

        /// <summary>
        /// Gets the filter clear command.
        /// </summary>
        public AsyncRelayCommand FilterClearCommand { get; }

        /// <summary>
        /// Gets the model collection.
        /// </summary>
        public ListCollectionView ModelCollection { get; }

        /// <summary>
        /// Gets the filter backends.
        /// </summary>
        public FilterOption[] FilterBackends { get; }

        /// <summary>
        /// Gets the filter pipelines.
        /// </summary>
        public FilterOption[] FilterPipelines { get; }

        /// <summary>
        /// Gets the filter statuses.
        /// </summary>
        public FilterOption[] FilterStatuses { get; }

        /// <summary>
        /// Gets or sets the selected model.
        /// </summary>
        public DiffusionModel SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); }
        }

        /// <summary>
        /// Gets or sets the filter text.
        /// </summary>
        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the filter backend.
        /// </summary>
        public FilterOption FilterBackend
        {
            get { return _filterBackend; }
            set { SetProperty(ref _filterBackend, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the filter pipeline.
        /// </summary>
        public FilterOption FilterPipeline
        {
            get { return _filterPipeline; }
            set { SetProperty(ref _filterPipeline, value); ModelCollection?.Refresh(); }
        }

        /// <summary>
        /// Gets or sets the filter status.
        /// </summary>
        public FilterOption FilterStatus
        {
            get { return _filterStatus; }
            set { SetProperty(ref _filterStatus, value); ModelCollection?.Refresh(); }
        }


        /// <summary>
        /// Called when the view is opened.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public override Task OpenAsync(OpenViewArgs args = null)
        {
            ModelCollection?.Refresh();
            return base.OpenAsync(args);
        }


        /// <summary>
        /// Creates the model collection filter.
        /// </summary>
        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not DiffusionModel model)
                    return false;

                if (model.MediaType != _mediaType)
                    return false;

                if (model.Id <= Utils.FixedIdRange && model.Status != ModelStatusType.Installed && model.Status != ModelStatusType.Unknown)
                    return false;

                var isvalid = true;
                if (!string.IsNullOrEmpty(_filterBackend?.Value))
                {
                    if (Enum.TryParse<BackendType>(_filterBackend.Value, out var backendType))
                    {
                        isvalid = isvalid && model.Backend == backendType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterStatus?.Value))
                {
                    if (Enum.TryParse<ModelStatusType>(_filterStatus.Value, out var statusType))
                    {
                        isvalid = isvalid && model.Status == statusType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterPipeline?.Value))
                {
                    isvalid = isvalid && model.Pipeline.ToString() == _filterPipeline.Value;
                }
                if (!string.IsNullOrEmpty(_filterText))
                {
                    isvalid = isvalid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                       || model.Pipeline.ToString().Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }

                return isvalid;
            };
        }


        /// <summary>
        /// Clears the filters.
        /// </summary>
        private Task FilterClearAsync()
        {
            FilterText = null;
            FilterBackend = FilterBackends[0];
            FilterStatus = FilterStatuses[0];
            FilterPipeline = FilterPipelines[0];
            return Task.CompletedTask;
        }


        /// <summary>
        /// Determines whether the filters can be cleared.
        /// </summary>
        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterBackend != FilterBackends[0]
                || _filterStatus != FilterStatuses[0]
                || _filterPipeline != FilterPipelines[0];
        }


        /// <summary>
        /// Opens the add model wizard.
        /// </summary>
        private async Task AddModelWizardAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelWizardDialog>();
            if (await dialog.AddAsync(_mediaType))
            {
                await SaveAsync();
                SelectedModel = dialog.SelectedTemplate;
            }
        }


        /// <summary>
        /// Copies the selected model.
        /// </summary>
        private async Task CopyModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.CopyAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.DiffusionModel;
            }
        }


        /// <summary>
        /// Updates the selected model.
        /// </summary>
        private async Task UpdateModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.UpdateAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.DiffusionModel;
            }
        }


        /// <summary>
        /// Removes the selected model.
        /// </summary>
        private async Task RemoveModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.RemoveModel, AppDefault.RemoveModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.DiffusionModels.Remove(SelectedModel);
                ModelCollection.MoveCurrentToFirst();
                SelectedModel = ModelCollection.CurrentItem as DiffusionModel;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Imports a model.
        /// </summary>
        private async Task ImportModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.ImportModel, filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelImports = await Json.LoadArrayAsync<DiffusionModel>(importPath);
                if (modelImports.IsNullOrEmpty())
                {
                    await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, AppErrors.ImportModelFailed);
                    return;
                }

                var modelDialog = DialogService.GetDialog<DiffusionModelDialog>();
                if (await modelDialog.ImportAsync(modelImports))
                {
                    await SaveAsync();
                }
            }
        }


        /// <summary>
        /// Exports the selected model.
        /// </summary>
        private async Task ExportModelAsync()
        {
            var exportPath = await DialogService.SaveFileAsync(AppDefault.ExportModel, $"{_selectedModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(exportPath))
            {
                await Json.SaveAsync<DiffusionModel>(exportPath, _selectedModel.DeepClone(0));
            }
        }


        /// <summary>
        /// Opens the selected model folder.
        /// </summary>
        private Task OpenModelAsync()
        {
            URL.NavigateToUrl(_selectedModel.GetDirectory(Settings));
            return Task.CompletedTask;
        }


        /// <summary>
        /// Deletes the selected model files.
        /// </summary>
        private async Task DeleteModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.DeleteModel, AppDefault.DeleteModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                if (!await ModelAccessService.RequestModelReleaseAsync(_selectedModel))
                    return;

                await DownloadService.CancelAsync(_selectedModel);
                await Task.Run(() => _selectedModel.Delete(Settings));
                _selectedModel.Status = ModelStatusType.Available;
                await SaveAsync();
            }
        }


        /// <summary>
        /// Queues the selected model download.
        /// </summary>
        private async Task DownloadModelAsync()
        {
            await DownloadService.DownloadAsync(_selectedModel);
        }


        /// <summary>
        /// Cancels the selected model download.
        /// </summary>
        private async Task DownloadModelCancelAsync()
        {
            await DownloadService.CancelAsync(_selectedModel);
        }


        /// <summary>
        /// Scans and saves the settings.
        /// </summary>
        private async Task SaveAsync()
        {
            Settings.ScanModels();
            await Settings.SaveAsync();
        }
    }
}
