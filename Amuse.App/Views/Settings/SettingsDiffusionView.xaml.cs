using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
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

        public SettingsDiffusionView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IModelDownloadService downloadService, IHistoryService historyService, ILogger<SettingsDiffusionView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
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
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Pipeline), ListSortDirection.Ascending));
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Name), ListSortDirection.Ascending));
            SelectedModel = settings.DiffusionModels.FirstOrDefault();
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


        public override Task OpenAsync(OpenViewArgs args = null)
        {
            return base.OpenAsync(args);
        }


        private Predicate<object> CollectionFilter()
        {
            return (obj) =>
            {
                if (obj is not DiffusionModel model)
                    return false;

                if (!string.IsNullOrEmpty(_filterText))
                {
                    return model.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                        || model.Pipeline.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            };
        }


        private Task FilterClearAsync()
        {
            FilterText = null;
            return Task.CompletedTask;
        }


        private bool CanClearFilter()
        {
            return !string.IsNullOrWhiteSpace(_filterText);
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
            if (await DialogService.ShowMessageAsync("Remove Model", $"Are you sure you want to remove this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.DiffusionModels.Remove(SelectedModel);
                SelectedModel = Settings.DiffusionModels.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportModelAsync()
        {
            var importPath = await DialogService.OpenFileAsync("Import Model", filter: "JSON |*.json;", defualtExt: "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var modelJson = await Json.LoadAsync<DiffusionModel>(importPath);
                if (modelJson == null)
                {
                    await DialogService.ShowMessageAsync("Import Error", "Failed to import model file.");
                    return;
                }

                var dialog = DialogService.GetDialog<DiffusionModelDialog>();
                if (await dialog.ImportAsync(modelJson))
                {
                    await SaveAsync();
                }
            }
        }


        private async Task ExportModelAsync()
        {
            var existingId = _selectedModel.Id;
            try
            {
                _selectedModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<DiffusionModel>(exportPath, _selectedModel);
                }
            }
            finally
            {
                _selectedModel.Id = existingId;
            }
        }


        private Task OpenModelAsync()
        {
            URL.NavigateToUrl(_selectedModel.GetDirectory(Settings.DirectoryModel));
            return Task.CompletedTask;
        }


        private async Task DeleteModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                await Task.Run(() => _selectedModel.Delete(Settings.DirectoryModel));
                _selectedModel.Status = ModelStatusType.Pending;
                await SaveAsync();
            }
        }


        private async Task DownloadModelAsync()
        {
            var isEnvironmentInstalled = EnvironmentService.IsInstalled();
            if (!isEnvironmentInstalled)
            {
                await DialogService.ShowErrorAsync("Environment Error", "No Environment Found, Please setup an environment and try again.");
                return;
            }
            await DownloadService.QueueAsync(_selectedModel, false);
        }


        private async Task DownloadModelCancelAsync()
        {
            await DownloadService.CancelAsync(_selectedModel);
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}