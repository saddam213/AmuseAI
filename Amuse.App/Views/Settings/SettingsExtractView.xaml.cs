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
    /// Interaction logic for SettingsExtractView.xaml
    /// </summary>
    public partial class SettingsExtractView : ViewBase
    {
        private ExtractModel _selectedModel;
        private string _filterText;
        private string _filterStatus;

        public SettingsExtractView(Settings settings, NavigationService navigationService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsExtractView> logger)
            : base(settings, navigationService, downloadService, historyService, logger)
        {
            FilterStatuses = ["Show All", "Installed", "Unknown"];
            FilterStatus = FilterStatuses[0];
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddModelCommand = new AsyncRelayCommand(AddModelAsync);
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
            ModelCollection = new ListCollectionView(settings.ExtractModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(ExtractModel.Pipeline), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(ExtractModel.Name), ListSortDirection.Ascending));
            ModelCollection.MoveCurrentToFirst();
            SelectedModel = ModelCollection.CurrentItem as ExtractModel;
            InitializeComponent();
        }

        public override View View => View.Extract;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddModelCommand { get; }
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
        public string[] FilterStatuses { get; }

        public ExtractModel SelectedModel
        {
            get { return _selectedModel; }
            set { SetProperty(ref _selectedModel, value); }
        }


        public string FilterText
        {
            get { return _filterText; }
            set { SetProperty(ref _filterText, value); ModelCollection?.Refresh(); }
        }

        public string FilterStatus
        {
            get { return _filterStatus; }
            set { SetProperty(ref _filterStatus, value); ModelCollection?.Refresh(); }
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
                if (obj is not ExtractModel model)
                    return false;

                if (model.Status != ModelStatusType.Installed && model.Status != ModelStatusType.Unknown)
                    return false;

                var isvalid = true;
                if (!string.IsNullOrEmpty(_filterStatus))
                {
                    if (Enum.TryParse<ModelStatusType>(_filterStatus, out var statusType))
                    {
                        isvalid = isvalid && model.Status == statusType;
                    }
                }
                if (!string.IsNullOrEmpty(_filterText))
                {
                    isvalid = isvalid && model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
                }
                return isvalid;
            };
        }


        private Task FilterClearAsync()
        {
            FilterText = null;
            FilterStatus = FilterStatuses[0];
            return Task.CompletedTask;
        }


        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText)
                || _filterStatus != FilterStatuses[0];
        }


        private async Task AddModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.AddAsync())
            {
                await SaveAsync();
            }
        }


        private Task AddModelWizardAsync()
        {
            return Task.CompletedTask; // TODO: Extract Wizard
        }


        private async Task CopyModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.CopyAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.ExtractModel;
            }
        }


        private async Task UpdateModelAsync()
        {
            var dialog = DialogService.GetDialog<ExtractModelDialog>();
            if (await dialog.UpdateAsync(SelectedModel))
            {
                await SaveAsync();
                SelectedModel = dialog.ExtractModel;
            }
        }


        private async Task RemoveModelAsync()
        {
            if (await DialogService.ShowMessageAsync(AppDefault.RemoveModel, AppDefault.RemoveModelMessage, TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.ExtractModels.Remove(SelectedModel);
                SelectedModel = Settings.ExtractModels.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync(AppDefault.ImportModel, filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelImports = await Json.LoadArrayAsync<ExtractModel>(importPath);
                if (modelImports.IsNullOrEmpty())
                {
                    await DialogService.ShowMessageAsync(AppErrors.ImportErrorTitle, AppErrors.ImportModel);
                    return;
                }

                var dialog = DialogService.GetDialog<ExtractModelDialog>();
                if (await dialog.ImportAsync(modelImports))
                {
                    await SaveAsync();
                    SelectedModel = dialog.ExtractModel;
                }
            }
        }


        private async Task ExportModelAsync()
        {
            var exportPath = await DialogService.SaveFileAsync(AppDefault.ExportModel, $"{_selectedModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(exportPath))
            {
                await Json.SaveAsync<ExtractModel>(exportPath, _selectedModel.DeepClone(0));
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