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
    /// Interaction logic for SettingsLanguageView.xaml
    /// </summary>
    public partial class SettingsLanguageView : ViewBase
    {
        private LanguageModel _selectedModel;
        private string _filterText;
        private FilterOption _filterBackend;
        private FilterOption _filterPipeline;
        private FilterOption _filterStatus;
        private FilterOption _filterMediaType;

        public SettingsLanguageView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IModelAccessService modelAccessService, IHistoryService historyService, ILogger<SettingsLanguageView> logger)
            : base(settings, navigationService, downloadService, modelAccessService, historyService, logger)
        {
            FilterBackends = [FilterOption.ShowAll(), .. Enum.GetValues<BackendType>().Where(x => x != BackendType.HuggingFace).Select(x => FilterOption.FromEnum(x))];
            FilterStatuses = FilterOption.ModelStatuses();
            FilterPipelines = [FilterOption.ShowAll(), .. settings.LanguagePipelines.Select(x => FilterOption.FromValue(x.ToString()))];
            FilterMediaTypes = [FilterOption.ShowAll(), .. Enum.GetValues<MediaType>().Select(x => FilterOption.FromEnum(x))];
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
            ModelCollection = new ListCollectionView(settings.LanguageModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(LanguageModel.Backend), ListSortDirection.Descending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(LanguageModel.Name), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(LanguageModel.Status), ListSortDirection.Descending));
            ModelCollection.MoveCurrentToFirst();
            SelectedModel = ModelCollection.CurrentItem as LanguageModel;
            InitializeComponent();
        }

        public override View View => View.SettingsLanguage;
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
        public FilterOption[] FilterBackends { get; }
        public FilterOption[] FilterPipelines { get; }
        public FilterOption[] FilterStatuses { get; }
        public FilterOption[] FilterMediaTypes { get; }

        public LanguageModel SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); }
        }

        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        public FilterOption FilterBackend
        {
            get { return _filterBackend; }
            set { SetProperty(ref _filterBackend, value); ModelCollection?.Refresh(); }
        }

        public FilterOption FilterPipeline
        {
            get { return _filterPipeline; }
            set { SetProperty(ref _filterPipeline, value); ModelCollection?.Refresh(); }
        }

        public FilterOption FilterStatus
        {
            get { return _filterStatus; }
            set { SetProperty(ref _filterStatus, value); ModelCollection?.Refresh(); }
        }

        public FilterOption FilterMediaType
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
                if (obj is not LanguageModel model)
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
                if (!string.IsNullOrEmpty(_filterMediaType?.Value))
                {
                    if (Enum.TryParse<MediaType>(_filterMediaType.Value, out var mediaType))
                    {
                        isvalid = isvalid && model.MediaType == mediaType;
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
            var dialog = DialogService.GetDialog<LanguageModelWizardDialog>();
            if (await dialog.ShowDialogAsync())
            {
                await SaveAsync();
                SelectedModel = dialog.SelectedTemplate;
            }
        }


        private async Task CopyModelAsync()
        {
            var dialog = DialogService.GetDialog<LanguageModelDialog>();
            if (await dialog.CopyAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.LanguageModel;
            }
        }


        private async Task UpdateModelAsync()
        {
            var dialog = DialogService.GetDialog<LanguageModelDialog>();
            if (await dialog.UpdateAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.LanguageModel;
            }
        }


        private async Task RemoveModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.RemoveModel, AppDefault.RemoveModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.LanguageModels.Remove(SelectedModel);
                SelectedModel = Settings.LanguageModels.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.ImportModel, filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelImports = await Json.LoadArrayAsync<LanguageModel>(importPath);
                if (modelImports.IsNullOrEmpty())
                {
                    await DialogService.ShowErrorAsync(AppErrors.ImportErrorTitle, AppErrors.ImportModelFailed);
                    return;
                }

                var modelDialog = DialogService.GetDialog<LanguageModelDialog>();
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
                await Json.SaveAsync<LanguageModel>(exportPath, _selectedModel.DeepClone(0));
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
                if (!await ModelAccessService.RequestModelReleaseAsync(_selectedModel))
                    return;

                await DownloadService.CancelAsync(_selectedModel);
                await Task.Run(() => _selectedModel.Delete(Settings));
                _selectedModel.Status = ModelStatusType.Available;
                await SaveAsync();
            }
        }


        private async Task DownloadModelAsync()
        {
            await DownloadService.DownloadAsync(_selectedModel);
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