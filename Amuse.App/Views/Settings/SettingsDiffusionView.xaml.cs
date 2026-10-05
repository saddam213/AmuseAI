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
    /// Interaction logic for SettingsDiffusionView.xaml
    /// </summary>
    public partial class SettingsDiffusionView : ViewBase
    {
        private DiffusionModel _selectedModel;
        private string _filterText;
        private string _filterBackend;
        private string _filterPipeline;
        private string _filterStatus;
        private string _filterMediaType;

        public SettingsDiffusionView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsDiffusionView> logger)
            : base(settings, navigationService, downloadService, historyService, logger)
        {
            FilterBackends = ["Show All", .. Enum.GetNames<BackendType>()];
            FilterStatuses = ["Show All", "Installed", "Unknown"];
            FilterPipelines = ["Show All", .. settings.DiffusionPipelines.Select(x => x.ToString()).Order()];
            FilterMediaTypes = ["Show All", .. Enum.GetNames<MediaType>()];
            FilterBackend = FilterBackends[0];
            FilterStatus = FilterStatuses[0];
            FilterPipeline = FilterPipelines[0];
            FilterMediaType = FilterMediaTypes[0];
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
            InitializeComponent();
        }

        public override View View => View.Diffusion;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddModelWizardCommand { get; }
        public AsyncRelayCommand CopyModelCommand { get; }
        public AsyncRelayCommand UpdateModelCommand { get; }
        public AsyncRelayCommand RemoveModelCommand { get; }
        public AsyncRelayCommand ImportModelCommand { get; }
        public AsyncRelayCommand ExportModelCommand { get; }
        public AsyncRelayCommand DeleteModelCommand { get; }
        public AsyncRelayCommand OpenModelCommand { get; }
        public AsyncRelayCommand DownloadModelCommand { get; }
        public AsyncRelayCommand DownloadModelCancelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }
        public string[] FilterBackends { get; }
        public string[] FilterPipelines { get; }
        public string[] FilterStatuses { get; }
        public string[] FilterMediaTypes { get; }

        public DiffusionModel SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        public string FilterBackend
        {
            get { return _filterBackend; }
            set { SetProperty(ref _filterBackend, value); ModelCollection?.Refresh(); }
        }

        public string FilterPipeline
        {
            get { return _filterPipeline; }
            set { SetProperty(ref _filterPipeline, value); ModelCollection?.Refresh(); }
        }

        public string FilterStatus
        {
            get { return _filterStatus; }
            set { SetProperty(ref _filterStatus, value); ModelCollection?.Refresh(); }
        }

        public string FilterMediaType
        {
            get { return _filterMediaType; }
            set { SetProperty(ref _filterMediaType, value); ModelCollection?.Refresh(); }
        }

        public override Task OpenAsync(OpenViewArgs args = null)
        {
            ModelCollection?.Refresh();
            return base.OpenAsync(args);
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not DiffusionModel model)
                    return false;

                if (model.Status != ModelStatusType.Installed && model.Status != ModelStatusType.Unknown)
                    return false;

                var isvalid = true;
                if (!string.IsNullOrEmpty(_filterBackend))
                {
                    if (Enum.TryParse<BackendType>(_filterBackend, out var backendType))
                    {
                        isvalid = isvalid && model.Backend == backendType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterStatus))
                {
                    if (Enum.TryParse<ModelStatusType>(_filterStatus, out var statusType))
                    {
                        isvalid = isvalid && model.Status == statusType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterMediaType))
                {
                    if (Enum.TryParse<MediaType>(_filterMediaType, out var mediaType))
                    {
                        isvalid = isvalid && model.MediaType == mediaType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterPipeline))
                {
                    isvalid = isvalid && (_filterPipeline == FilterPipelines[0] || model.Pipeline.ToString() == _filterPipeline);
                }
                if (!string.IsNullOrEmpty(_filterText))
                {
                    isvalid = isvalid && (model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                                       || model.Pipeline.ToString().Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }

                return isvalid;
            };
        }


        private Task FilterClearAsync()
        {
            FilterText = null;
            FilterBackend = FilterBackends[0];
            FilterStatus = FilterStatuses[0];
            FilterPipeline = FilterPipelines[0];
            FilterMediaType = FilterMediaTypes[0];
            return Task.CompletedTask;
        }


        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterBackend != FilterBackends[0]
                || _filterStatus != FilterStatuses[0]
                || _filterPipeline != FilterPipelines[0]
                || _filterMediaType != FilterMediaTypes[0];
        }


        private async Task AddModelWizardAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelWizardDialog>();
            if (await dialog.ShowDialogAsync())
            {
                await SaveAsync();
                SelectedModel = dialog.SelectedTemplate;
            }
        }


        private async Task CopyModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.CopyAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.DiffusionModel;
            }
        }


        private async Task UpdateModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.UpdateAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.DiffusionModel;
            }
        }


        private async Task RemoveModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.RemoveModel, AppDefault.RemoveModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.DiffusionModels.Remove(SelectedModel);
                SelectedModel = Settings.DiffusionModels.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.ImportModel, filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelImports = await Json.LoadArrayAsync<DiffusionModel>(importPath);
                if (modelImports.IsNullOrEmpty())
                {
                    await DialogService.ShowMessageAsync(AppErrors.ImportErrorTitle, AppErrors.ImportModel);
                    return;
                }

                var modelDialog = DialogService.GetDialog<DiffusionModelDialog>();
                if (await modelDialog.ImportAsync(modelImports))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportModelAsync()
        {
            var exportPath = await DialogService.SaveFileAsync(AppDefault.ExportModel, $"{_selectedModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(exportPath))
            {
                await Json.SaveAsync<DiffusionModel>(exportPath, _selectedModel.DeepClone(0));
            }
        }


        private Task OpenModelAsync()
        {
            URL.NavigateToUrl(_selectedModel.GetDirectory(Settings));
            return Task.CompletedTask;
        }


        private async Task DeleteModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.DeleteModel, AppDefault.DeleteModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                await Task.Run(() => _selectedModel.Delete(Settings));
                _selectedModel.Status = ModelStatusType.Available;
                await SaveAsync();
            }
        }


        private async Task DownloadModelAsync()
        {
            await DownloadService.QueueAsync(_selectedModel);
        }


        private async Task DownloadModelCancelAsync()
        {
            await DownloadService.CancelAsync(_selectedModel);
        }


        private async Task SaveAsync()
        {
            Settings.ScanModels();
            await Settings.SaveAsync();
        }
    }
}