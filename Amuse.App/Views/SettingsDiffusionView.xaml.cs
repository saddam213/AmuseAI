using Amuse.App.Common;
using Amuse.App.Dialogs;
using Amuse.App.Services;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
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
        private DiffusionModel _selectedDiffusionModel;
        private string _filterText;



        public SettingsDiffusionView(Settings settings, NavigationService navigationService, IEnvironmentService environmentService, IDownloadService downloadService, IHistoryService historyService, ILogger<SettingsDiffusionView> logger)
            : base(settings, navigationService, environmentService, downloadService, historyService, logger)
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            AddDiffusionModelWizardCommand = new AsyncRelayCommand(AddDiffusionModelWizardAsync);
            CopyDiffusionModelCommand = new AsyncRelayCommand(CopyDiffusionModelAsync, () => SelectedDiffusionModel is not null);
            UpdateDiffusionModelCommand = new AsyncRelayCommand(UpdateDiffusionModelAsync, () => SelectedDiffusionModel?.Id > Utils.FixedIdRange);
            RemoveDiffusionModelCommand = new AsyncRelayCommand(RemoveDiffusionModelAsync, () => SelectedDiffusionModel?.Id > Utils.FixedIdRange);
            ImportDiffusionModelCommand = new AsyncRelayCommand(ImportDiffusionModelAsync);
            ExportDiffusionModelCommand = new AsyncRelayCommand(ExportDiffusionModelAsync, () => SelectedDiffusionModel is not null);
            DownloadDiffusionModelCommand = new AsyncRelayCommand(DownloadDiffusionModelAsync);
            DownloadDiffusionModelCancelCommand = new AsyncRelayCommand(DownloadDiffusionModelCancelAsync);
            FilterClearCommand = new AsyncRelayCommand(FilterClearAsync, CanClearFilter);
            ModelCollection = new ListCollectionView(settings.DiffusionModels) { Filter = CollectionFilter(), IsLiveSorting = true };
            ModelCollection.SortDescriptions.Add(new SortDescription(nameof(DiffusionModel.Name), ListSortDirection.Ascending));
            SelectedDiffusionModel = settings.DiffusionModels.FirstOrDefault();
            InitializeComponent();
        }

        public override View View => View.Diffusion;
        public AsyncRelayCommand SaveCommand { get; }
        public AsyncRelayCommand AddDiffusionModelWizardCommand { get; }
        public AsyncRelayCommand CopyDiffusionModelCommand { get; }
        public AsyncRelayCommand UpdateDiffusionModelCommand { get; }
        public AsyncRelayCommand RemoveDiffusionModelCommand { get; }
        public AsyncRelayCommand ImportDiffusionModelCommand { get; }
        public AsyncRelayCommand ExportDiffusionModelCommand { get; }
        public AsyncRelayCommand DownloadDiffusionModelCommand { get; }
        public AsyncRelayCommand DownloadDiffusionModelCancelCommand { get; }
        public AsyncRelayCommand FilterClearCommand { get; }
        public ListCollectionView ModelCollection { get; }

        public DiffusionModel SelectedDiffusionModel
        {
            get { return _selectedDiffusionModel; }
            set { SetProperty(ref _selectedDiffusionModel, value); }
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


        private async Task AddDiffusionModelWizardAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelWizardDialog>();
            if (await dialog.ShowDialogAsync())
            {
                await SaveAsync();
                SelectedDiffusionModel = dialog.SelectedTemplate;
            }
        }


        private async Task CopyDiffusionModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.CopyAsync(SelectedDiffusionModel))
            {
                await SaveAsync();
                SelectedDiffusionModel = dialog.DiffusionModel;
            }
        }


        private async Task UpdateDiffusionModelAsync()
        {
            var dialog = DialogService.GetDialog<DiffusionModelDialog>();
            if (await dialog.UpdateAsync(SelectedDiffusionModel))
            {
                await SaveAsync();
                SelectedDiffusionModel = dialog.DiffusionModel;
            }
        }


        private async Task RemoveDiffusionModelAsync()
        {
            if (await DialogService.ShowMessageAsync("Delete Model", $"Are you sure you want to delete this model?", TensorStack.WPF.Dialogs.MessageDialogType.YesNo, TensorStack.WPF.Dialogs.MessageBoxIconType.Warning, TensorStack.WPF.Dialogs.MessageBoxStyleType.Danger))
            {
                Settings.DiffusionModels.Remove(SelectedDiffusionModel);
                SelectedDiffusionModel = Settings.DiffusionModels.FirstOrDefault();
                await SaveAsync();
            }
        }


        private async Task ImportDiffusionModelAsync()
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


        private async Task ExportDiffusionModelAsync()
        {
            var existingId = _selectedDiffusionModel.Id;
            try
            {
                _selectedDiffusionModel.Id = 0;
                var exportPath = await DialogService.SaveFileAsync("Export Model", $"{_selectedDiffusionModel.Name}.json", filter: "JSON |*.json;", defualtExt: "json");
                if (!string.IsNullOrEmpty(exportPath))
                {
                    await Json.SaveAsync<DiffusionModel>(exportPath, _selectedDiffusionModel);
                }
            }
            finally
            {
                _selectedDiffusionModel.Id = existingId;
            }
        }


        private async Task DownloadDiffusionModelAsync()
        {
            var isEnvironmentInstalled = EnvironmentService.IsInstalled();
            if (!isEnvironmentInstalled)
            {
                await DialogService.ShowErrorAsync("Environment Error", "No Environment Found, Please setup an environment and try again.");
                return;
            }
            await DownloadService.QueueAsync(_selectedDiffusionModel, false);
        }


        private async Task DownloadDiffusionModelCancelAsync()
        {
            await DownloadService.CancelAsync(_selectedDiffusionModel);
        }


        private async Task SaveAsync()
        {
            await SettingsManager.SaveAsync(Settings);
            Settings.ScanModels();
        }
    }
}